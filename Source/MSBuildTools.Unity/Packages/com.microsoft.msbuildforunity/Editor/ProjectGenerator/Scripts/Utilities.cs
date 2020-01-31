// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// Represents where a Unity project reference asset is located.
    /// </summary>
    public enum AssetLocation
    {
        /// <summary>
        /// Inside the Assets folder of the Unity project.
        /// </summary>
        Project,

        /// <summary>
        /// Inside the Package folder (not the PackagesCache)
        /// </summary>
        Package,

        /// <summary>
        /// Inside the copy of the PackagesCache folder of the Unity project.
        /// </summary>
        PackageLibraryCache,

        /// <summary>
        /// Inside the Packages folder shipped with the Unity version.
        /// </summary>
        BuiltInPackage,

        /// <summary>
        /// The asset is located external to any known projects, but is somehow pulled in.
        /// </summary>
        External
    }

    /// <summary>
    /// Helper Utilities methods used by other classes.
    /// </summary>
    public static class Utilities
    {
        private const string AssetsFolderName = "Assets";
        public const string PackagesFolderName = "Packages";
        private const string PackagesCacheFolderName = "PackageCache";
        private const string MSBuildFolderName = "MSBuild";
        private const string MSBuildProjectsFolderName = "Projects";

#if UNITY_EDITOR_OSX
        private const string BuiltInPackagesRelativePath = @"Unity.app/Contents/Resources/PackageManager/BuiltInPackages";
#else
        private const string BuiltInPackagesRelativePath = @"Data\Resources\PackageManager\BuiltInPackages";
#endif
        public static string ProjectPath { get; } = GetNormalizedPath(Application.dataPath.Substring(0, Application.dataPath.Length - AssetsFolderName.Length), true);
        public static string MSBuildOutputFolder { get; } = GetNormalizedPath(ProjectPath + MSBuildFolderName, true);
        public static string MSBuildProjectFolder { get; } = GetNormalizedPath(Path.Combine(MSBuildOutputFolder, MSBuildProjectsFolderName));
        public static string PackageLibraryCachePath { get; } = GetNormalizedPath(Path.Combine(ProjectPath, "Library", PackagesCacheFolderName));

        public const string MetaFileGuidRegex = @"guid:\s*([0-9a-fA-F]{32})";

        public static string PackagesPath { get; } = GetNormalizedPath(Path.Combine(ProjectPath, PackagesFolderName));

        public static string AssetPath { get; } = GetNormalizedPath(Application.dataPath, true);

        public static string BuiltInPackagesPath { get; } = GetNormalizedPath(Path.Combine(Path.GetDirectoryName(EditorApplication.applicationPath), BuiltInPackagesRelativePath), true);

        /// <summary>
        /// Converts an assets relative path to an absolute path.
        /// </summary>
        public static string GetFullPathFromAssetsRelative(string assetsRelativePath)
        {
            if (assetsRelativePath.StartsWith(AssetsFolderName))
            {
                return Path.GetFullPath(AssetPath + assetsRelativePath.Substring(AssetsFolderName.Length));
            }

            throw new InvalidOperationException("Not a path known to be relative to the project's Asset folder.");
        }

        /// <summary>
        /// Converts a Packages relative path to an absolute path using PackagesCopy directory instead.
        /// </summary>
        public static string GetFullPathFromPackagesRelative(string path)
        {
            if (path.StartsWith(PackagesFolderName))
            {
                // This is weird special Unity behaviour, the GetFullPath will replace the "Packages" path to "PackagesCache", which we don't want.
                // However, a File.Exists works the same way; we only care about the packages that are trully in the <ProjectFolder>/Packages instead of <ProjectFolder>/Library/PackagesCache
                string fullPathInPackages = Path.GetFullPath(Path.Combine(ProjectPath, path));
                if (!fullPathInPackages.Contains("PackageCache") && (File.Exists(fullPathInPackages) || Directory.Exists(fullPathInPackages)))
                {
                    // The Packages folder really has these items, they aren't virtual and thus won't be part of PackagesCopy folder
                    return fullPathInPackages;
                }

                return fullPathInPackages;// Path.GetFullPath(PackagesCopyPath + path.Substring(PackagesFolderName.Length));
            }

            throw new InvalidOperationException("Not a path known to be relative to project's Package folder.");
        }

        /// <summary>
        /// Gets whether the given path is visible to Unity.
        /// </summary>
        public static bool IsVisibleToUnity(string path)
        {
            return GetAssetImporter(path) != null;
        }

        private static AssetImporter GetAssetImporter(string path)
        {
            path = Path.GetFullPath(path);

            string relativePath;

            if (path.StartsWith(AssetPath))
            {
                relativePath = path.Replace(AssetPath, AssetsFolderName);
            }
            else if (path.StartsWith(PackageLibraryCachePath))
            {
                relativePath = path.Replace(PackageLibraryCachePath, PackagesFolderName);
                // Relative path will contain packages with their version "@x.y.z", so we need to strip that
                // Method is to split on '@' into 'Packages\com.company.package' and 'x.y.z\<Path>' and then substring to first '\'
                if (relativePath.Contains('@'))
                {
                    string[] parts = relativePath.Split('@');
                    relativePath = parts[0] + parts[1].Substring(parts[1].IndexOf('\\'));
                }
            }
            else if (path.Contains(PackagesPath))
            {
                relativePath = path.Replace(PackagesPath, PackagesFolderName);
            }
            else
            {
                return null;
            }

            return string.IsNullOrEmpty(relativePath)
                ? null
                : AssetImporter.GetAtPath(relativePath);
        }

        /// <summary>
        /// Parses a .meta file to extract a guid for the asset.
        /// </summary>
        /// <param name="assetPath">The path to the asset (not the .meta file).</param>
        /// <param name="guid">The guid extracted.</param>
        /// <returns>True if the operation was succesful.</returns>
        public static bool TryGetGuidForAsset(FileInfo assetPath, out Guid guid)
        {
            string metaFile = $"{assetPath.FullName}.meta";

            if (!File.Exists(metaFile))
            {
                guid = default;
                return false;
            }

            string guidString = null;
            using (StreamReader reader = new StreamReader(metaFile))
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    Match match = Regex.Match(line, Utilities.MetaFileGuidRegex);

                    if (match.Success)
                    {
                        guidString = match.Groups[1].Captures[0].Value;
                        break;
                    }
                }
            }

            if (guid != null && Guid.TryParse(guidString, out guid))
            {
                return true;
            }

            guid = default;
            return false;
        }

        /// <summary>
        /// Gets the known <see cref="AssetLocation"/> for the asset file.
        /// </summary>
        /// <param name="assetFile">The asset file.</param>
        /// <returns>The <see cref="AssetLocation"/> if valid; throws an exception otherwise.</returns>
        public static AssetLocation GetAssetLocation(FileSystemInfo assetFile)
        {
            string absolutePath = Path.GetFullPath(assetFile.FullName);

            if (absolutePath.Contains(AssetPath))
            {
                return AssetLocation.Project;
            }
            else if (absolutePath.Contains(PackagesPath) && assetFile.Exists)
            {
                return AssetLocation.Package;
            }
            else if (absolutePath.Contains(PackagesPath) || absolutePath.Contains(PackageLibraryCachePath))
            {
                return AssetLocation.PackageLibraryCache;
            }
            else if (absolutePath.Contains(BuiltInPackagesPath))
            {
                return AssetLocation.BuiltInPackage;
            }
            else
            {
                return AssetLocation.External;
            }
        }

        /// <summary>
        /// Gets a full path from one of the two known relative paths (Assets, Packages). Packages is converted to use PackagesCopy.
        /// </summary>
        public static string GetFullPathFromKnownRelative(string path)
        {
            if (path.StartsWith(AssetsFolderName))
            {
                return GetFullPathFromAssetsRelative(path);
            }
            else if (path.StartsWith(PackagesFolderName))
            {
                return GetFullPathFromPackagesRelative(path);
            }

            throw new InvalidOperationException("Not a known path relative to project's folders.");
        }

        /// <summary>
        /// Get a path relative to the assets folder from the absolute path.
        /// </summary>
        public static string GetAssetsRelativePathFrom(string absolutePath)
        {
            absolutePath = Path.GetFullPath(absolutePath);

            if (absolutePath.Contains(AssetPath))
            {
                return absolutePath.Replace(AssetPath, AssetsFolderName);
            }

            throw new ArgumentException(nameof(absolutePath), $"Absolute path '{absolutePath}' is not a Unity Assets relative path ('{AssetPath}')");
        }

        /// <summary>
        /// Gets a relative path between two absolute paths.
        /// </summary>
        public static string GetRelativePath(string thisAbsolute, string thatAbsolute)
        {
            if (!thisAbsolute.EndsWith("\\"))
            {
                thisAbsolute = thisAbsolute + "\\";
            }

            return GetNormalizedPath(Uri.UnescapeDataString(new Uri(thisAbsolute).MakeRelativeUri(new Uri(thatAbsolute)).OriginalString));
        }

        /// <summary>
        /// Gets a relative path between two absolute paths.
        /// </summary>
        public static Uri GetRelativeUri(string thisAbsolute, string thatAbsolute)
        {
            if (!thisAbsolute.EndsWith("\\"))
            {
                thisAbsolute = thisAbsolute + "\\";
            }

            return new Uri(thisAbsolute).MakeRelativeUri(new Uri(thatAbsolute));
        }

        /// <summary>
        /// Converts a relative Uri to a properly formatted string.
        /// </summary>
        public static string AsRelativePath(this Uri uri)
        {
            if (uri.IsAbsoluteUri)
            {
                throw new ArgumentException("Expecting a relative Uri.", nameof(uri));
            }

            return GetNormalizedPath(Uri.UnescapeDataString(uri.OriginalString));
        }

        /// <summary>
        /// Gets a relative path between two known relative paths (inside Assets or Packages)
        /// </summary>
        public static string GetRelativePathForKnownFolders(string thisKnownFolder, string thatKnownFolder)
        {
            return GetRelativePath(GetFullPathFromKnownRelative(thisKnownFolder), GetFullPathFromKnownRelative(thatKnownFolder));
        }

        /// <summary>
        /// Reads until some contents is encountered, or the end of the stream is reached.
        /// </summary>
        /// <param name="reader">The <see cref="StreamReader"/> to use for reading.</param>
        /// <param name="contents">The contents to search for in the lines being read.</param>
        /// <returns>The line on which some of the contents was found.</returns>
        public static string ReadUntil(this StreamReader reader, params string[] contents)
        {
            return ReadWhile(reader, line => !contents.Any(c => line.Contains(c)));
        }

        /// <summary>
        /// Helper extension method to add all items to a collection.
        /// </summary>
        /// <typeparam name="T">Type of item.</typeparam>
        /// <param name="collection">Collection to add items to.</param>
        /// <param name="items">The items to add.</param>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            foreach (T item in items)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// A helper to check whether a DLL is a managed assembly.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly.</param>
        /// <remarks>Taken from https://stackoverflow.com/questions/367761/how-to-determine-whether-a-dll-is-a-managed-assembly-or-native-prevent-loading. </remarks>
        /// <returns>True if a managed assembly.</returns>
        public static bool IsManagedAssembly(string assemblyPath)
        {
            assemblyPath = Path.GetFullPath(assemblyPath);

            if (GetAssetImporter(assemblyPath) is PluginImporter importer)
            {
                return !importer.isNativePlugin;
            }

            using (Stream fileStream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader binaryReader = new BinaryReader(fileStream))
            {
                if (fileStream.Length < 64)
                {
                    return false;
                }

                //PE Header starts @ 0x3C (60). Its a 4 byte header.
                fileStream.Position = 0x3C;
                uint peHeaderPointer = binaryReader.ReadUInt32();
                if (peHeaderPointer == 0)
                {
                    peHeaderPointer = 0x80;
                }

                // Ensure there is at least enough room for the following structures:
                //     24 byte PE Signature & Header
                //     28 byte Standard Fields         (24 bytes for PE32+)
                //     68 byte NT Fields               (88 bytes for PE32+)
                // >= 128 byte Data Dictionary Table
                if (peHeaderPointer > fileStream.Length - 256)
                {
                    return false;
                }

                // Check the PE signature.  Should equal 'PE\0\0'.
                fileStream.Position = peHeaderPointer;
                uint peHeaderSignature = binaryReader.ReadUInt32();
                if (peHeaderSignature != 0x00004550)
                {
                    return false;
                }

                // skip over the PEHeader fields
                fileStream.Position += 20;

                const ushort PE32 = 0x10b;
                const ushort PE32Plus = 0x20b;

                // Read PE magic number from Standard Fields to determine format.
                ushort peFormat = binaryReader.ReadUInt16();
                if (peFormat != PE32 && peFormat != PE32Plus)
                {
                    return false;
                }

                // Read the 15th Data Dictionary RVA field which contains the CLI header RVA.
                // When this is non-zero then the file contains CLI data otherwise not.
                ushort dataDictionaryStart = (ushort)(peHeaderPointer + (peFormat == PE32 ? 232 : 248));
                fileStream.Position = dataDictionaryStart;

                uint cliHeaderRva = binaryReader.ReadUInt32();
                if (cliHeaderRva == 0)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Reads while the predicate is satisifed, returns the line on which it failed.
        /// </summary>
        /// <param name="reader">The <see cref="StreamReader"/> to use for reading.</param>
        /// <param name="predicate">The predicate that should return false when reading should stop.</param>
        /// <returns>The line on which the predicate returned false.</returns>
        public static string ReadWhile(this StreamReader reader, System.Func<string, bool> predicate)
        {
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (!predicate(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Copies the source directory to the output directory creating all the directories first then copying.
        /// </summary>
        /// <param name="extensionFilters">This list of extensions allow for double extensions such as .cs.meta.</param>
        public static void CopyDirectory(string sourcePath, string destinationPath, params string[] extensionFilters)
        {
            // Create the root directory itself
            Directory.CreateDirectory(destinationPath);

            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                if (extensionFilters != null && extensionFilters.Length > 0)
                {
                    foreach (string extensionFilter in extensionFilters)
                    {
                        if (newPath.EndsWith(extensionFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                }

                File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
            }
        }

        /// <summary>
        /// Gets a normalized path (with slashes matching the platform), and optionally can make full path.
        /// </summary>
        public static string GetNormalizedPath(string path, bool makeFullPath = false)
        {
            if (makeFullPath)
            {
                path = Path.GetFullPath(path);
            }

            return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Deletes a directory then waits for it to be flushed in the system as deleted before creating. Sometimes deleting and creating to quickly will result in an exception.
        /// </summary>
        public static void EnsureCleanDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                DeleteDirectory(path, true);
            }

            if (!TryIOWithRetries(() => Directory.CreateDirectory(path), 5, TimeSpan.FromMilliseconds(30)))
            {
                throw new InvalidOperationException($"Failed to create the directory at '{path}'.");
            }
        }

        /// <summary>
        /// Helper to perform an IO operation with retries.
        /// </summary>
        public static bool TryIOWithRetries(Action operation, int numRetries, TimeSpan sleepBetweenRetrie, bool throwOnLastRetry = false)
        {
            do
            {
                try
                {
                    operation();
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    if (throwOnLastRetry && numRetries == 0)
                    {
                        throw;
                    }
                }
                catch (IOException)
                {
                    if (throwOnLastRetry && numRetries == 0)
                    {
                        throw;
                    }
                }

                Thread.Sleep(sleepBetweenRetrie);
                numRetries--;
            } while (numRetries >= 0);

            return false;
        }

        /// <summary>
        /// Delete directory helper that also waits for delete to completely propogate through the system.
        /// </summary>
        public static void DeleteDirectory(string targetDir, bool waitForDirectoryDelete = false)
        {
            File.SetAttributes(targetDir, FileAttributes.Normal | FileAttributes.Hidden);

            string[] files = Directory.GetFiles(targetDir);
            string[] dirs = Directory.GetDirectories(targetDir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir, waitForDirectoryDelete);
            }

            TryIOWithRetries(() => Directory.Delete(targetDir, false), 2, TimeSpan.FromMilliseconds(100), true);

            if (waitForDirectoryDelete)
            {
#if UNITY_EDITOR // Just in case make sure this is forced to be Editor only
                // Sometimes the delete isn't committed fast enough, lets spin and wait for this to happen
                for (int i = 0; i < 10 && Directory.Exists(targetDir); i++)
                {
                    Thread.Sleep(100);
                }
#endif
            }
        }

        /// <summary>
        /// Helper to replace tokens in text using StringBuilder.
        /// </summary>
        public static string ReplaceTokens(string text, Dictionary<string, string> tokens, bool verifyAllTokensPresent = false)
        {
            if (verifyAllTokensPresent)
            {
                string[] missingTokens = tokens.Keys.Where(t => !text.Contains(t)).ToArray();
                if (missingTokens.Length > 0)
                {
                    throw new InvalidOperationException($"Token replacement failed, found tokens missing from the template: '{string.Join("; ", missingTokens)}'.");
                }
            }

            StringBuilder builder = new StringBuilder(text);

            foreach (KeyValuePair<string, string> token in tokens)
            {
                if (!string.IsNullOrEmpty(token.Key))
                {
                    builder.Replace(token.Key, token.Value);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Helper to fetch an XML based template.
        /// </summary>
        public static bool TryGetXMLTemplate(string text, string templateName, out string template)
        {
            string regex = $"(<!--{templateName}_TEMPLATE_START-->.*<!--{templateName}_TEMPLATE_END-->)";
            Match result = Regex.Match(text, regex, RegexOptions.Singleline);

            if (result.Success && result.Groups[1].Success && result.Groups[1].Captures.Count > 0)
            {
                template = result.Groups[1].Captures[0].Value;
                return true;
            }

            template = null;
            return false;
        }

        /// <summary>
        /// Given a list of Asset guids converts them to asset paths in place.
        /// </summary>
        public static void GetPathsFromGuidsInPlace(string[] guids, bool fullPaths = false)
        {
            for (int i = 0; i < guids.Length; i++)
            {
                guids[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (fullPaths)
                {
                    guids[i] = GetFullPathFromKnownRelative(guids[i]);
                }
            }
        }

        /// <summary>
        /// Helper to see if the specified BuildTarget is installed in the editor.
        /// </summary>
        public static bool IsPlatformInstalled(BuildTarget buildTarget)
        {
            try
            {
                return BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(buildTarget), buildTarget);
            }
            catch (PlatformNotSupportedException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the value for a specific key if it's part of the dictionary, otherwise creates a new value and sets it using the given factory function.
        /// </summary>
        /// <typeparam name="TKey">The type of key.</typeparam>
        /// <typeparam name="TValue">The type of value.</typeparam>
        /// <param name="this">The dictionary.</param>
        /// <param name="key">They key to check for.</param>
        /// <param name="factoryFunc">The factory func that will be given a key, if a new value needs to be created and added.</param>
        /// <returns>The fetched or added value.</returns>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> factoryFunc)
        {
            if (!@this.TryGetValue(key, out TValue toReturn))
            {
                @this[key] = toReturn = factoryFunc(key);
            }

            return toReturn;
        }

        public static IEnumerable<Version> GetUWPSDKs()
        {
#if UNITY_EDITOR
            Type uwpReferences = Type.GetType("UnityEditor.Scripting.Compilers.UWPReferences, UnityEditor.dll");
            MethodInfo getInstalledSDKS = uwpReferences.GetMethod("GetInstalledSDKs", BindingFlags.Static | BindingFlags.Public);
            MethodInfo sdkVersionToString = uwpReferences.GetMethod("SdkVersionToString", BindingFlags.Static | BindingFlags.NonPublic);

            IEnumerable<object> uwpSDKS = (IEnumerable<object>)getInstalledSDKS.Invoke(null, new object[0]);

            return uwpSDKS.Select(t => (Version)t.GetType().GetField("Version", BindingFlags.Instance | BindingFlags.Public).GetValue(t));
#else
            throw new PlatformNotSupportedException($"{nameof(GetUWPSDKs)} is only supported in the editor.");
#endif
        }
    }
}
#endif