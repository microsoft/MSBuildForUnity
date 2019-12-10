// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// A helper class to parse the state of the current Unity project.
    /// </summary>
    public class UnityProjectInfo : IDisposable
    {
        /// <summary>
        /// These package references aren't actual packages it appears, manually labeling them for exclusion.
        /// </summary>
        private static readonly HashSet<string> ExcludedPackageReferences = new HashSet<string>()
        {
            "Windows.UI.Input.Spatial"
        };

        /// <summary>
        /// Gets the name of this Unity Project.
        /// </summary>
        public string UnityProjectName { get; }

        /// <summary>
        /// Gets the available platforms for this Unity project.
        /// </summary>
        public IReadOnlyCollection<CompilationPlatformInfo> AvailablePlatforms { get; }

        /// <summary>
        /// Gets the special editor platform.
        /// </summary>
        public CompilationPlatformInfo EditorPlatform { get; }

        /// <summary>
        /// Gets the current player platform.
        /// </summary>
        public CompilationPlatformInfo CurrentPlayerPlatform { get; }

        /// <summary>
        /// Gets all the parsed CSProjects for this Unity project.
        /// </summary>
        public IReadOnlyDictionary<string, CSProjectInfo> CSProjects { get; private set; }

        /// <summary>
        /// Gets all the parsed DLLs for this Unity project.
        /// </summary>
        public IReadOnlyCollection<PluginAssemblyInfo> Plugins { get; private set; }

        public UnityProjectInfo(HashSet<BuildTarget> supportedBuildTargets)
        {
            AvailablePlatforms = new ReadOnlyCollection<CompilationPlatformInfo>(CompilationPipeline.GetAssemblyDefinitionPlatforms()
                    .Where(t => supportedBuildTargets.Contains(t.BuildTarget))
                    .Select(CompilationPlatformInfo.GetCompilationPlatform)
                    .OrderBy(t => t.Name).ToList());

            EditorPlatform = CompilationPlatformInfo.GetEditorPlatform();

            UnityProjectName = Application.productName;

            if (string.IsNullOrWhiteSpace(UnityProjectName))
            {
                UnityProjectName = "UnityProject";
            }

            RefreshPlugins();
            RefreshProjects();

            CurrentPlayerPlatform = AvailablePlatforms.First(t => t.BuildTarget == EditorUserBuildSettings.activeBuildTarget);
        }

        public void Dispose()
        {
            // This will be used soon
        }

        public void RefreshPlugins()
        {
            Plugins = new ReadOnlyCollection<PluginAssemblyInfo>(ScanForPluginDLLs());

            foreach (PluginAssemblyInfo plugin in Plugins)
            {
                if (plugin.Type == PluginType.Native)
                {
                    // Logging will be re-enabled with robust update holistically across MSB4U: https://github.com/microsoft/MSBuildForUnity/issues/75
                    // Debug.Log($"Native plugin {plugin.ReferencePath.AbsolutePath} not yet supported for MSBuild project.");
                }
            }

            RefreshProjects();
        }

        public void RefreshProjects()
        {
            CSProjects = new ReadOnlyDictionary<string, CSProjectInfo>(CreateUnityProjects());
        }

        private Dictionary<string, CSProjectInfo> CreateUnityProjects()
        {
            // Not all of these will be converted to C# objects, only the ones found to be referenced
            Dictionary<string, AssemblyDefinitionInfo> asmDefInfoMap = new Dictionary<string, AssemblyDefinitionInfo>();
            SortedSet<AssemblyDefinitionInfo> asmDefDirectoriesSorted = new SortedSet<AssemblyDefinitionInfo>(Comparer<AssemblyDefinitionInfo>.Create((a, b) => a.Directory.FullName.CompareTo(b.Directory.FullName)));

            HashSet<string> builtInPackagesWithoutSource = new HashSet<string>();

            // Parse the builtInPackagesFirst
            DirectoryInfo builtInPackagesDirectory = new DirectoryInfo(Utilities.BuiltInPackagesPath);
            foreach (DirectoryInfo packageDirectory in builtInPackagesDirectory.GetDirectories())
            {
                FileInfo[] asmDefFiles = packageDirectory.GetFiles("*.asmdef", SearchOption.AllDirectories);

                if (asmDefFiles.Length == 0)
                {
                    builtInPackagesWithoutSource.Add(packageDirectory.Name.ToLower());
                    continue;
                }

                foreach (FileInfo fileInfo in asmDefFiles)
                {
                    AssemblyDefinitionInfo assemblyDefinitionInfo = AssemblyDefinitionInfo.Parse(fileInfo, this, null, true);
                    asmDefDirectoriesSorted.Add(assemblyDefinitionInfo);
                    asmDefInfoMap.Add(Path.GetFileNameWithoutExtension(fileInfo.Name), assemblyDefinitionInfo);
                }
            }

            Dictionary<string, string> packageCacheVersionedMap = new Dictionary<string, string>();
            foreach (string directory in Directory.GetDirectories(Utilities.PackageLibraryCachePath))
            {
                string directoryName = Path.GetFileName(directory);
                packageCacheVersionedMap.Add(directoryName.Split('@')[0], directoryName);
            }

            Dictionary<string, Assembly> unityAssemblies = CompilationPipeline.GetAssemblies().ToDictionary(t => t.name);
            Dictionary<string, CSProjectInfo> projectsMap = new Dictionary<string, CSProjectInfo>();
            Queue<string> projectsToProcess = new Queue<string>();
            // Parse the unity assemblies
            foreach (KeyValuePair<string, Assembly> pair in unityAssemblies)
            {
                if (!asmDefInfoMap.TryGetValue(pair.Key, out AssemblyDefinitionInfo assemblyDefinitionInfo))
                {
                    string asmDefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(pair.Key);
                    if (string.IsNullOrEmpty(asmDefPath))
                    {
                        if (!pair.Key.StartsWith("Assembly-CSharp"))
                        {
                            throw new InvalidOperationException($"Failed to retrieve AsmDef for script assembly: {pair.Key}");
                        }

                        assemblyDefinitionInfo = AssemblyDefinitionInfo.GetDefaultAssemblyCSharpInfo(pair.Value);
                        projectsToProcess.Enqueue(pair.Key);
                    }
                    else
                    {
                        assemblyDefinitionInfo = AssemblyDefinitionInfo.Parse(new FileInfo(Utilities.GetFullPathFromKnownRelative(asmDefPath)), this, pair.Value);

                        if (asmDefPath.StartsWith("Assets/"))
                        {
                            // Add as mandatory
                            projectsToProcess.Enqueue(pair.Key);
                        }
                    }

                    asmDefDirectoriesSorted.Add(assemblyDefinitionInfo);
                    asmDefInfoMap.Add(pair.Key, assemblyDefinitionInfo);
                }
            }

            // This will parse additional asmdefs that are not part of current compilation set, but we still need
            foreach (string asmdefGuid in AssetDatabase.FindAssets("t:asmdef"))
            {
                string asmDefPath = AssetDatabase.GUIDToAssetPath(asmdefGuid);
                string asmDefKey = Path.GetFileNameWithoutExtension(asmDefPath);
                if (!asmDefInfoMap.ContainsKey(asmDefKey))
                {
                    AssemblyDefinitionInfo assemblyDefinitionInfo = AssemblyDefinitionInfo.Parse(new FileInfo(Utilities.GetFullPathFromKnownRelative(asmDefPath)), this, null);
                    asmDefDirectoriesSorted.Add(assemblyDefinitionInfo);
                    asmDefInfoMap.Add(asmDefKey, assemblyDefinitionInfo);
                }
            }

            int index = 0;
            ProcessSortedAsmDef(asmDefDirectoriesSorted.ToArray(), ref index, (uri) => true, (a) => { });

            while (projectsToProcess.Count > 0)
            {
                string projectKey = projectsToProcess.Dequeue();

                if (!projectsMap.ContainsKey(projectKey))
                {
                    GetProjectInfo(projectsMap, asmDefInfoMap, builtInPackagesWithoutSource, projectKey);
                }
            }

            return projectsMap;
        }

        private void ProcessSortedAsmDef(AssemblyDefinitionInfo[] set, ref int currentIndex, Func<Uri, bool> childOfParentFunc, Action<AssemblyDefinitionInfo> addAsChild)
        {
            Uri GetUri(DirectoryInfo d) => d.FullName.EndsWith("\\") ? new Uri(d.FullName) : new Uri(d.FullName + "\\");

            for (; currentIndex < set.Length;)
            {
                AssemblyDefinitionInfo current = set[currentIndex];
                addAsChild(current);

                if (currentIndex + 1 == set.Length)
                {
                    return;
                }

                currentIndex++;

                AssemblyDefinitionInfo next = set[currentIndex];

                Uri potentialBase = GetUri(current.Directory);
                Uri potentialChild = GetUri(next.Directory);
                if (!childOfParentFunc(potentialChild))
                {
                    return;
                }
                else if (potentialBase.IsBaseOf(potentialChild))
                {
                    ProcessSortedAsmDef(set, ref currentIndex, potentialBase.IsBaseOf, (a) => current.NestedAssemblyDefinitionFiles.Add(a));
                    if (!childOfParentFunc(potentialChild))
                    {
                        return;
                    }
                }
            }
        }

        private CSProjectInfo GetProjectInfo(Dictionary<string, CSProjectInfo> projectsMap, Dictionary<string, AssemblyDefinitionInfo> asmDefInfoMap, HashSet<string> builtInPackagesWithoutSource, string projectKey)
        {
            if (projectKey.StartsWith("GUID:"))
            {
                projectKey = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(projectKey.Substring("GUID:".Length)));
            }

            if (projectsMap.TryGetValue(projectKey, out CSProjectInfo value))
            {
                return value;
            }

            if (!asmDefInfoMap.TryGetValue(projectKey, out AssemblyDefinitionInfo assemblyDefinitionInfo))
            {
                Debug.Log($"Can't find an asmdef for project: {projectKey}; Unity actually allows this, so proceeding.");
                return null;
            }

            CSProjectInfo toReturn = new CSProjectInfo(this, assemblyDefinitionInfo);
            projectsMap.Add(projectKey, toReturn);

            if (!assemblyDefinitionInfo.BuiltInPackage)
            {
                Uri dependencies = new Uri(Path.Combine(Utilities.AssetPath, "Dependencies"));
                foreach (PluginAssemblyInfo plugin in Plugins.Where(t => t.Type != PluginType.Native))
                {
                    if (!dependencies.IsBaseOf(plugin.ReferencePath) && (plugin.AutoReferenced || assemblyDefinitionInfo.PrecompiledAssemblyReferences.Contains(plugin.Name)))
                    {
                        toReturn.AddDependency(plugin);
                    }
                }
            }

            foreach (string reference in toReturn.AssemblyDefinitionInfo.References)
            {
                if (ExcludedPackageReferences.Contains(reference))
                {
                    Debug.LogWarning($"Skipping processing {reference} for {toReturn.Name}, as it's marked as excluded.");
                    continue;
                }
                string packageCandidate = $"com.{reference.ToLower()}";
                if (builtInPackagesWithoutSource.Any(t => packageCandidate.StartsWith(t)))
                {
                    Debug.LogWarning($"Skipping processing {reference} for {toReturn.Name}, as it's a built-in package without source.");
                    continue;
                }

                CSProjectInfo dependencyToAdd = GetProjectInfo(projectsMap, asmDefInfoMap, builtInPackagesWithoutSource, reference);
                if (dependencyToAdd != null)
                {
                    toReturn.AddDependency(dependencyToAdd);
                }
            }

            return toReturn;
        }

        private List<PluginAssemblyInfo> ScanForPluginDLLs()
        {
            List<PluginAssemblyInfo> toReturn = new List<PluginAssemblyInfo>();

            foreach (string assetAssemblyPath in Directory.GetFiles(Utilities.AssetPath, "*.dll", SearchOption.AllDirectories))
            {
                string assetRelativePath = Utilities.GetAssetsRelativePathFrom(assetAssemblyPath);
                PluginImporter importer = (PluginImporter)AssetImporter.GetAtPath(assetRelativePath);
                if (importer == null)
                {
                    Debug.LogWarning($"Didn't get an importer for '{assetRelativePath}', most likely due to it being in a Unity hidden folder (prefixed by a .)");
                    continue;
                }

                PluginAssemblyInfo toAdd = new PluginAssemblyInfo(this, Guid.Parse(AssetDatabase.AssetPathToGUID(assetRelativePath)), assetAssemblyPath, importer.isNativePlugin ? PluginType.Native : PluginType.Managed);
                toReturn.Add(toAdd);
            }

            foreach (string packageDllPath in Directory.GetFiles(Utilities.PackageLibraryCachePath, "*.dll", SearchOption.AllDirectories))
            {
                string metaPath = packageDllPath + ".meta";

                if (!File.Exists(metaPath))
                {
                    Debug.LogWarning($"Skipping a packages DLL that didn't have an associated meta: '{packageDllPath}'");
                    continue;
                }
                Guid guid;
                using (StreamReader reader = new StreamReader(metaPath))
                {
                    string guidLine = reader.ReadUntil("guid");
                    if (!Guid.TryParse(guidLine.Split(':')[1].Trim(), out guid))
                    {
                        Debug.LogWarning($"Skipping a packages DLL that didn't have a valid guid in the .meta file: '{packageDllPath}'");
                        continue;
                    }
                }

                bool isManaged = Utilities.IsManagedAssembly(packageDllPath);
                PluginAssemblyInfo toAdd = new PluginAssemblyInfo(this, guid, packageDllPath, isManaged ? PluginType.Managed : PluginType.Native);
                toReturn.Add(toAdd);
            }

            return toReturn;
        }
    }
}
#endif