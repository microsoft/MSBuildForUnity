// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MRW.Shared.Build.UnityApp.Tasks
{
    /// <summary>
    /// Remap the paths to source files within a PDB file. This is useful for scenarios where you cannot use Source Indexing or Source Linking,
    /// such as when dealing with Unity debugging.
    /// </summary>
    public sealed class RemapSymbolSources : Task
    {
        [Required]
        public ITaskItem[] Assemblies { get; set; }

        public ITaskItem[] ReferenceAssemblySearchPaths { get; set; }

        private const float SUCCESSFULLY_MAPPED_THRESHOLD = 0.5f;

        private const string METADATA_SOURCE_ROOT = "SourceRoot";
        private const char WINDOWS_DIRECTORY_SEPERATOR = '\\';

        private class PathReplacement
        {
            /// <summary>
            /// The determined root path of source files referenced by the symbols
            /// </summary>
            public string RootPathInSymbols;

            /// <summary>
            /// The determined root path on disk of the source files in their new location
            /// </summary>
            public string RootPathOnDisk;
        }

        public override bool Execute()
        {
            Log.LogMessage("Beginning to process assemblies for source path remapping");

            // Add additional search paths to the assembly resolver if they were given
            DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
            if (ReferenceAssemblySearchPaths != null)
            {
                foreach (ITaskItem searchPath in ReferenceAssemblySearchPaths)
                {
                    assemblyResolver.AddSearchDirectory(searchPath.ItemSpec);
                }
            }

            foreach (ITaskItem assembly in Assemblies)
            {
                try
                {
                    Log.LogMessage($"Processing assembly \"{assembly.ItemSpec}\"");

                    string assemblyPath = assembly.ItemSpec;
                    string sourceRoot = assembly.GetMetadata(METADATA_SOURCE_ROOT);

                    if (String.IsNullOrEmpty(sourceRoot))
                    {
                        Log.LogMessage($"No source path was given for \"{assemblyPath}\". In order to process this assembly provide the SourcePath as a metadata attribute. No action will be taken.");
                        continue;
                    }

                    if (!Directory.Exists(sourceRoot))
                    {
                        Log.LogMessage($"Could not access source root path \"{sourceRoot}\" for assembly \"{assemblyPath}\". No action will be taken.");
                        continue;
                    }

                    // Make sure the sourceRoot is an absolute path
                    sourceRoot = Path.GetFullPath(sourceRoot);

                    // Ensure the sourcePath has a trailing slash (it's needed for relative file comparisons later)
                    if (!sourceRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    {
                        sourceRoot += Path.DirectorySeparatorChar;
                    }

                    // Open up a single stream for both the read and write operation
                    using (var filestream = new FileStream(assemblyPath, FileMode.Open, FileAccess.ReadWrite))
                    {
                        AssemblyDefinition assemblyDefinition = null;

                        try
                        {
                            // Read the assembly and provide the flag so the symbols are read in as well
                            assemblyDefinition = AssemblyDefinition.ReadAssembly(filestream, new ReaderParameters { ReadSymbols = true, AssemblyResolver = assemblyResolver });
                        }
                        catch (SymbolsNotFoundException)
                        {
                            Log.LogMessage($"Could not find symbols for \"{assemblyPath}\". No action will be taken.");
                            continue;
                        }

                        // Only the Windows platform is capable of writing Native PDBs. If we're not on windows and the symbols are native log a message and move along.
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && assemblyDefinition.MainModule?.SymbolReader is NativePdbReader)
                        {
                            Log.LogMessage($"Assembly \"{assemblyPath}\" uses Windows debug symbols (.pdb). They can only be modified on Windows devices. Consider changing the library to use Portable debug symbols.");
                            continue;
                        }

                        // Parse out all of the documents (target source files) found in the assembly/symbols
                        IReadOnlyCollection<Document> documentReferences = GetAllDocumentsInAssembly(assemblyDefinition);

                        if (documentReferences.Count == 0)
                        {
                            Log.LogMessage($"Assembly \"{assemblyPath}\" doesn't appear to have any source references. No action will be taken.");
                            continue;
                        }

                        PathReplacement pathReplacement = DeterminePathReplacement(sourceRoot, documentReferences);

                        if (pathReplacement == null) // The path mapping couldn't be determined
                        {
                            Log.LogMessage($"Could not map references from \"{assemblyPath}\" to \"{sourceRoot}\". No action will be taken.");
                            continue;
                        }

                        Log.LogMessage($"Source references for assembly \"{assemblyPath}\" will be remapped from \"{pathReplacement.RootPathInSymbols}\" to \"{pathReplacement.RootPathOnDisk}\"");

                        // Keep a list of unsuccessful mappings for reporting at the end
                        List<string> unsuccessfulRemappedDocuments = new List<string>();

                        // Update each document in the assembly with the new path mapping
                        foreach (Document document in documentReferences)
                        {
                            // Remove the length of the symbol path replacement from the beginning of the PDB path (+1 to get the directory seperator as well). Then 
                            // combine it with the new root path on disk.
                            string relativePathNormalized = document.Url.Substring(pathReplacement.RootPathInSymbols.Length + 1).Replace(WINDOWS_DIRECTORY_SEPERATOR, Path.DirectorySeparatorChar);
                            string remappedPath = Path.Combine(pathReplacement.RootPathOnDisk, relativePathNormalized);

                            if (File.Exists(remappedPath))
                            {
                                document.Url = remappedPath;
                            }
                            else
                            {
                                // The remapped file does not exist. It might be a file that never lived in the source repository. Record the failure.
                                unsuccessfulRemappedDocuments.Add(document.Url);
                            }
                        }

                        float successfulRemapPercentage = (documentReferences.Count - unsuccessfulRemappedDocuments.Count) / (float)documentReferences.Count;
                        Log.LogMessage($"{successfulRemapPercentage:P2} of references in assembly \"{assemblyPath}\" were successfully remapped.");

                        if (unsuccessfulRemappedDocuments.Count > 0)
                        {
                            Log.LogMessage($"The following files could not be remapped:\n    {String.Join("\n    ", unsuccessfulRemappedDocuments)}");
                        }

                        // Now update the assembly/pdb with the new debug information
                        assemblyDefinition.Write(filestream, new WriterParameters { WriteSymbols = true });
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Exception encountered when processing assembly \"{assembly.ItemSpec}\". See the stack trace below for more information.");
                    Log.LogWarningFromException(ex, showStackTrace:true);
                }

            }

            // Always return passing. The only failure case with be if an unhandled exception was thrown.
            return true;
        }

        /// <summary>
        /// Calculate the mapping between where the sourceRoot is and where the library thinks source files
        /// are. As long as the sourceRoot is a valid location there should be a common path within the assembly
        /// that needs to be replaced for all sources.
        /// </summary>
        private PathReplacement DeterminePathReplacement(string sourceRoot, IReadOnlyCollection<Document> documentReferences)
        {
            foreach (Document referencedFile in documentReferences)
            {
                // The paths in the PDB may be in the Unix format or Windows format. Windows accepts either forward or backward slash.
                // Unix only supports forward slashes, so replace any backslashes with the OS directory seperator, this will
                // effectively do nothing on Windows.
                string normalizedPathInSymbols = referencedFile.Url.Replace(WINDOWS_DIRECTORY_SEPERATOR, Path.DirectorySeparatorChar);

                // Search in the destination source root for any files of the same name.
                string[] potentialSourceFiles = Directory.GetFiles(sourceRoot, Path.GetFileName(normalizedPathInSymbols), SearchOption.AllDirectories);

                foreach (string potentialSourceFile in potentialSourceFiles)
                {
                    // Check if the file with the same name is really the same file the PDB expects.
                    if (!FileMatchesDocumentReference(referencedFile, potentialSourceFile))
                    {
                        // The file is not the same one referenced by the document, move along to the next one.
                        continue;
                    }

                    // The file on disk is the same file referenced by the symbols. Keep removing directories off the
                    // end of the two paths until they diverge. The remainder should give us the path we need to replace
                    // in the symbols and what path on disk we need to replace it with.
                    // EX:
                    //   PathInSymbols -> C:\BA\143\S\Scripts\Foo.cs
                    //   PathOnDisk    -> E:\Repos\ugui-mvvm\Scripts\Foo.cs
                    //
                    // If the last elements of the path match remove them and try again
                    //   Foo.cs == Foo.cs
                    //   PathInSymbols -> C:\BA\143\S\Scripts
                    //   PathOnDisk    -> E:\Repos\ugui-mvvm\Scripts
                    //
                    //   Scripts == Scripts
                    //   PathInSymbols -> C:\BA\143\S
                    //   PathOnDisk    -> E:\Repos\ugui-mvvm
                    //
                    //   S != ugui-mvvm
                    //
                    // We then know that the root path in the symbols C:\BA\143\S should be replaced with E:\Repos\ugui-mvvm to map
                    // between the two.

                    string rootPathInSymbols = normalizedPathInSymbols;
                    string rootPathOnDisk = potentialSourceFile;

                    while(Path.GetFileName(rootPathInSymbols).Equals(Path.GetFileName(rootPathOnDisk), StringComparison.OrdinalIgnoreCase))
                    {
                        rootPathInSymbols = Path.GetDirectoryName(rootPathInSymbols);
                        rootPathOnDisk = Path.GetDirectoryName(rootPathOnDisk);
                    }

                    // We may have normalized the rootPathInSymbols for the manipulations. Swap it back with the correct substring
                    // the original document url
                    rootPathInSymbols = referencedFile.Url.Substring(0, rootPathInSymbols.Length);

                    return new PathReplacement() { RootPathInSymbols = rootPathInSymbols, RootPathOnDisk = rootPathOnDisk };
                }
            }

            // No source mapping could be determined, this most likely means there either isn't any source in the package or we were given an unrelated SourceRoot.
            return null;
        }

        /// <summary>
        /// Determines if the file given is the same file that the document references. It does the comparison
        /// using the file hash provided in the document against the computed hash of the file on disk.
        /// </summary>
        private bool FileMatchesDocumentReference(Document doc, String file)
        {
            HashAlgorithm hashingAlgorithm = null;
            FileStream fileStream = null;

            try
            {
                switch (doc.HashAlgorithm)
                {
                    case DocumentHashAlgorithm.MD5:
                        hashingAlgorithm = MD5.Create();
                        break;
                    case DocumentHashAlgorithm.SHA1:
                        hashingAlgorithm = SHA1.Create();
                        break;
                    case DocumentHashAlgorithm.SHA256:
                        hashingAlgorithm = SHA256.Create();
                        break;
                    case DocumentHashAlgorithm.None:
                    default:
                        // The file either doesn't have a hash or it's an unknown type of hash. In either case return false for no match.
                        return false;
                }

                if (!File.Exists(file))
                {
                    // The file doesn't exist, it can't be a match
                    return false;
                }

                fileStream = File.OpenRead(file);
                byte[] computedHash = hashingAlgorithm.ComputeHash(fileStream);

                return computedHash.SequenceEqual(doc.Hash);
            }
            finally
            {
                // Cleanup our IDisposable objects
                hashingAlgorithm?.Dispose();
                fileStream?.Dispose();
            }
        }

        /// <summary>
        /// Iterate over the given assembly and find all unique documents
        /// </summary>
        /// <returns></returns>
        private IReadOnlyCollection<Document> GetAllDocumentsInAssembly(AssemblyDefinition assembly)
        {
            HashSet<Document> uniqueDocuments = new HashSet<Document>();

            // Unfortunately the list of documents referenced by an assembly is not exposed in a convenient manner. The only way
            // to access the documents is by enumerating all sequence points (mappings of memory addresses to files) and obtain
            // their reference documents. Any document can be referenced by multiple sequence points, so you must also weed out
            // the duplicates, which is easy because they all share the same object reference.
            return new HashSet<Document>
            (
                from module in assembly.Modules
                from type in module.GetTypes()
                from method in type.Methods
                from sequencePoint in method.DebugInformation.SequencePoints
                where sequencePoint.Document != null
                select sequencePoint.Document
            );
        }
    }
}
