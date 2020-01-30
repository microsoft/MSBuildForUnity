// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
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
    public enum UnityConfigurationType
    {
        InEditor,
        Player
    }

    /// <summary>
    /// A helper class to parse the state of the current Unity project.
    /// </summary>
    public class UnityProjectInfo
    {
        /// <summary>
        /// These package references aren't actual packages it appears, manually labeling them for exclusion.
        /// </summary>
        private static readonly HashSet<string> ExcludedPackageReferences = new HashSet<string>()
        {
            "Windows.UI.Input.Spatial"
        };

        /// <summary>
        /// For some Unity packages, references don't match the appropriate asmdef name.
        /// </summary>
        private static readonly Dictionary<string, string> ProjectAliases = new Dictionary<string, string>()
        {
            { "Unity.ugui", "UnityEngine.UI" },
            { "Unity.ugui.Editor", "UnityEditor.UI" }
        };

        /// <summary>
        /// Another patching technique to add defines to some assembly defintion files. TestRunner for example, is only referenced by projects with UNITY_INCLUDE_TESTS and references nunit that has UNITY_INCLUDE_TESTS;
        /// However it doesn't have the define itself. This breaks Player build, and as it appears that Unity specially handles this case as well.
        /// </summary>
        private static readonly Dictionary<string, List<string>> ImpliedDefinesForAsmDefs = new Dictionary<string, List<string>>()
        {
            { "UnityEditor.TestRunner", new List<string>(){ "UNITY_INCLUDE_TESTS" } },
            { "UnityEngine.TestRunner", new List<string>(){ "UNITY_INCLUDE_TESTS" } },
        };

        private readonly ILogger logger;
        private readonly MSBuildToolsConfig config;

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

        /// <summary>
        /// Gets all the parsed winmds for this Unity project.
        /// </summary>
        public IReadOnlyCollection<WinMDInfo> WinMDs { get; private set; }

        /// <summary>
        /// Existing projects that are found under the Assets folder, or as part of a UPM Package.
        /// </summary>
        public IReadOnlyCollection<string> ExistingCSProjects { get; private set; }

        /// <summary>
        /// Parses the current state of the Unity project.
        /// </summary>
        /// <param name="supportedBuildTargets">BuildTargets that are considered supported.</param>
        /// <param name="config">Config for MSBuildTools.</param>
        /// <param name="performCompleteParse">If this is false, UnityProjectInfo will parse only the minimum required information about current project. Includes: <see cref="ExistingCSProjects"/> <see cref="CurrentPlayerPlatform"/>.</param>
        public UnityProjectInfo(ILogger logger, Dictionary<BuildTarget, string> supportedBuildTargets, MSBuildToolsConfig config, bool performCompleteParse = true)
        {
            this.logger = logger;
            this.config = config;

            if (performCompleteParse)
            {
                AvailablePlatforms = new ReadOnlyCollection<CompilationPlatformInfo>(CompilationPipeline.GetAssemblyDefinitionPlatforms()
                        .Where(t => Utilities.IsPlatformInstalled(t.BuildTarget))
                        .Where(t => supportedBuildTargets.ContainsKey(t.BuildTarget))
                        .Select(CompilationPlatformInfo.GetCompilationPlatform)
                        .OrderBy(t => t.Name).ToList());

                EditorPlatform = CompilationPlatformInfo.GetEditorPlatform();

                CurrentPlayerPlatform = AvailablePlatforms.First(t => t.BuildTarget == EditorUserBuildSettings.activeBuildTarget);
            }
            else
            {
                CurrentPlayerPlatform = CompilationPlatformInfo.GetCompilationPlatform(
                    CompilationPipeline.GetAssemblyDefinitionPlatforms()
                        .First(t => t.BuildTarget == EditorUserBuildSettings.activeBuildTarget));
            }

            UnityProjectName = Application.productName;

            if (string.IsNullOrWhiteSpace(UnityProjectName))
            {
                UnityProjectName = "UnityProject";
            }

            RefreshPlugins(performCompleteParse);

            if (performCompleteParse)
            {
                RefreshProjects();
            }
        }

        public void RefreshPlugins(bool performCompleteParse)
        {
            List<PluginAssemblyInfo> plugins = new List<PluginAssemblyInfo>();
            List<WinMDInfo> winmds = new List<WinMDInfo>();
            List<string> existingCSProjectFiles = new List<string>();

            Dictionary<string, Action<string, Guid>> scanMap = new Dictionary<string, Action<string, Guid>>();

            if (performCompleteParse)
            {
                scanMap.Add(".dll", (path, guid) => plugins.Add(new PluginAssemblyInfo(this, guid, path, Utilities.IsManagedAssembly(path) ? PluginType.Managed : PluginType.Native)));
                scanMap.Add(".winmd", (path, guid) => winmds.Add(new WinMDInfo(this, guid, path)));
            }

            scanMap.Add(".csproj", (path, guid) =>
            {
                if (!path.EndsWith(".msb4u.csproj"))
                {
                    existingCSProjectFiles.Add(path);
                }
            });

            ScanAndProcessKnownFolders(scanMap);

            if (performCompleteParse)
            {
                Plugins = new ReadOnlyCollection<PluginAssemblyInfo>(plugins);
                WinMDs = new ReadOnlyCollection<WinMDInfo>(winmds);

                // Logging will be re-enabled with robust update holistically across MSB4U: https://github.com/microsoft/MSBuildForUnity/issues/75
                //foreach (PluginAssemblyInfo plugin in Plugins)
                //{
                //    if (plugin.Type == PluginType.Native)
                //    {
                //        Debug.Log($"Native plugin {plugin.ReferencePath.AbsolutePath} not yet supported for MSBuild project.");
                //    }
                //}
            }

            ExistingCSProjects = new ReadOnlyCollection<string>(existingCSProjectFiles);
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

                        Guid guid;
                        switch (pair.Key)
                        {
                            case "Assembly-CSharp":
                                guid = config.AssemblyCSharpGuid;
                                break;
                            case "Assembly-CSharp-firstpass":
                                guid = config.AssemblyCSharpFirstPassGuid;
                                break;
                            case "Assembly-CSharp-Editor":
                                guid = config.AssemblyCSharpEditorGuid;
                                break;
                            case "Assembly-CSharp-Editor-firstpass":
                                guid = config.AssemblyCSharpFirstPassEditorGuid;
                                break;
                            default:
                                throw new InvalidOperationException($"Predefined assembly '{assemblyDefinitionInfo.Name}' was not recognized, this generally means it should be added to the switch statement in CSProjectInfo:GetProjectType.");
                        }

                        assemblyDefinitionInfo = AssemblyDefinitionInfo.GetDefaultAssemblyCSharpInfo(pair.Value, guid);
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

            // Now we have all of the assembly definiton files, let's run a quick validation. 
            ValidateAndPatchAssemblyDefinitions(asmDefInfoMap);

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

        /// <summary>
        /// This performs reference correction, for example this corrects "Unity.ugui" to be "UnityEngine.UI" (a known error of TextMeshPro). For correction map see <see cref="ProjectAliases"/>.
        /// </summary>
        private void ValidateAndPatchAssemblyDefinitions(Dictionary<string, AssemblyDefinitionInfo> asmDefInfoMap)
        {
            foreach (KeyValuePair<string, AssemblyDefinitionInfo> asmDefPair in asmDefInfoMap)
            {
                for (int i = 0; i < asmDefPair.Value.References.Length; i++)
                {
                    string reference = asmDefPair.Value.References[i];
                    if (!asmDefInfoMap.ContainsKey(reference))
                    {
                        if (ProjectAliases.TryGetValue(reference, out string correctedReference))
                        {
                            Debug.Log($"Correcting package '{reference}' to '{correctedReference}'.");
                            asmDefPair.Value.References[i] = correctedReference;
                        }
                    }
                }

                if (ImpliedDefinesForAsmDefs.TryGetValue(asmDefPair.Key, out List<string> defines))
                {
                    foreach (string define in defines)
                    {
                        asmDefPair.Value.DefineConstraints.Add(define);
                    }
                }
            }
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
                Uri dependencies = new Uri(Path.Combine(Utilities.AssetPath, "Dependencies\\"));
                foreach (PluginAssemblyInfo plugin in Plugins.Where(t => t.Type != PluginType.Native))
                {
                    if (!dependencies.IsBaseOf(plugin.ReferencePath) && (plugin.AutoReferenced || assemblyDefinitionInfo.PrecompiledAssemblyReferences.Contains(plugin.Name)))
                    {
                        toReturn.AddDependency(plugin);
                    }
                }

                foreach (WinMDInfo winmd in WinMDs)
                {
                    if (!dependencies.IsBaseOf(winmd.ReferencePath))
                    {
                        if (winmd.IsValid)
                        {
                            toReturn.AddDependency(winmd);
                        }
                        else
                        {
                            Debug.LogError($"References to {winmd} were excluded because the winmd is configured incorrectly. Make sure this winmd is setup to only support WSAPlayer in the Unity inspector.");
                        }
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

        private IEnumerable<KeyValuePair<string, string>> ScanForFiles(string folder, IEnumerable<string> extensions)
        {
            HashSet<string> extensionSet = new HashSet<string>(extensions.Select(t => t.ToLower()));
            foreach (string file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if (extensionSet.Contains(extension))
                {
                    yield return new KeyValuePair<string, string>(extension, file);
                }
            }
        }

        private void ScanAndProcessKnownFolders(Dictionary<string, Action<string, Guid>> extensionCallbacks)
        {
            ScanAndProcessFiles(Utilities.AssetPath, extensionCallbacks);
            ScanAndProcessFiles(Utilities.PackageLibraryCachePath, extensionCallbacks);
        }

        private void ScanAndProcessFiles(string folder, Dictionary<string, Action<string, Guid>> extensionCallbacks)
        {
            foreach (KeyValuePair<string, string> pair in ScanForFiles(folder, extensionCallbacks.Keys))
            {
                if (!Utilities.IsVisibleToUnity(pair.Value))
                {
                    Debug.LogWarning($"Skipping processing asset '{pair.Value}' as it's not visible to Unity.");
                }
                else if (!Utilities.TryGetGuidForAsset(new FileInfo(pair.Value), out Guid guid))
                {
                    Debug.LogWarning($"Skipping processing asset '{pair.Value}' as no meta file was found, or guid parsed.");
                }
                else
                {
                    extensionCallbacks[pair.Key](pair.Value, guid);
                }
            }
        }

        #region Export Logic
        public void ExportSolution(IUnityProjectExporter unityProjectExporter, FileInfo solutionFilePath, DirectoryInfo generatedProjectsFolder)
        {
            SolutionFileInfo solutionFileInfo = TextSolutionFileParser.ParseExistingSolutionFile(logger, solutionFilePath.FullName);
            ISolutionExporter exporter = unityProjectExporter.CreateSolutionExporter(logger, solutionFilePath);

            //TODO we need to figure out how to handle existing projects

            // Remove known folders
            solutionFileInfo.Projects.Remove(config.BuiltInPackagesFolderGuid);
            solutionFileInfo.Projects.Remove(config.ImportedPackagesFolderGuid);
            solutionFileInfo.Projects.Remove(config.ExternalPackagesFolderGuid);

            // Process generated projects
            foreach (KeyValuePair<string, CSProjectInfo> projectPair in CSProjects)
            {
                Uri relativePath = Utilities.GetRelativeUri(solutionFilePath.Directory.FullName, MSBuildUnityProjectExporter.GetProjectPath(projectPair.Value, generatedProjectsFolder).FullName);
                exporter.AddProject(CreateSolutionProjectEntry(projectPair.Value, relativePath, solutionFileInfo), isGenerated: true);
            }

            // Add dependency project
            string dependencyProjectName = $"{UnityProjectName}.Dependencies.msb4u";
            exporter.AddProject(GetDependencyProjectReference(dependencyProjectName, $"{dependencyProjectName}.csproj"), isGenerated: true);

            // Process existing projects
            foreach (KeyValuePair<Guid, Project> projectPair in solutionFileInfo.Projects)
            {
                if (!exporter.Projects.ContainsKey(projectPair.Key)
                    && !solutionFileInfo.MSB4UGeneratedItems.Contains(projectPair.Key)
                    && projectPair.Value.TypeGuid != SolutionProject.FolderTypeGuid)
                {
                    exporter.AddProject(GetSolutionProjectEntryFrom(projectPair.Value, solutionFileInfo));
                }
            }

            // Bring forward the properties, then set the one we care about
            exporter.Properties.AddRange(solutionFileInfo.Properties);
            exporter.Properties["HideSolutionNode"] = "FALSE";

            // Bring forward the extensibility globals, then set the one we care about
            exporter.ExtensibilityGlobals.AddRange(solutionFileInfo.ExtensibilityGlobals);
            exporter.ExtensibilityGlobals["SolutionGuid"] = "{" + config.SolutionGuid.ToString().ToUpper() + "}";

            // Bring forward the pairs, and then set the platforms we know of
            exporter.ConfigurationPlatforms.AddRange(solutionFileInfo.ConfigPlatformPairs);
            foreach (CompilationPlatformInfo platform in AvailablePlatforms)
            {
                exporter.ConfigurationPlatforms.Add(new ConfigurationPlatformPair(UnityConfigurationType.InEditor, platform.Name));
                exporter.ConfigurationPlatforms.Add(new ConfigurationPlatformPair(UnityConfigurationType.Player, platform.Name));
            }

            // Set the folders by scanning for "projects" with folder type in old projects
            foreach (KeyValuePair<Guid, Project> folderPair in solutionFileInfo.Projects.Where(t => t.Value.TypeGuid == SolutionProject.FolderTypeGuid))
            {
                exporter.GetOrAddFolder(folderPair.Key, folderPair.Value.Name)
                    .Children.AddRange(solutionFileInfo.ChildToParentNestedMappings.Where(t => t.Value == folderPair.Key).Select(t => t.Key));
            }

            Dictionary<AssetLocation, Tuple<Guid, string>> knownFolderMapping = GetKnownFolderMapping();

            // Confiure known folders for generated projects
            ProcessSetForKnownFolders(exporter, knownFolderMapping, CSProjects.Values, t => t.AssemblyDefinitionInfo.AssetLocation, t => t.Guid);

            //TODO handle existing projects

            exporter.AdditionalSections = solutionFileInfo.SolutionSections;

            exporter.Write();
        }

        private SolutionProject GetDependencyProjectReference(string projectName, string relativePath)
        {
            SolutionProject toReturn = new SolutionProject(config.DependenciesProjectGuid, SolutionProject.CSharpProjectTypeGuid, projectName, relativePath, null, CSProjects.Values.Select(t => t.Guid));

            void SetMapping(ConfigurationPlatformPair configPlatform) => toReturn.ConfigurationPlatformMapping.Add(configPlatform, new ProjectConfigurationPlatformMapping() { ConfigurationPlatform = configPlatform, EnabledForBuild = true });

            SetMapping(new ConfigurationPlatformPair("Debug", "Any CPU"));
            SetMapping(new ConfigurationPlatformPair("Release", "Any CPU"));

            return toReturn;
        }

        private void ProcessSetForKnownFolders<T>(ISolutionExporter exporter, Dictionary<AssetLocation, Tuple<Guid, string>> knownFolderMapping, IEnumerable<T> set, Func<T, AssetLocation> getAssetLocation, Func<T, Guid> getGuidFunc)
        {
            foreach (T item in set)
            {
                if (knownFolderMapping.TryGetValue(getAssetLocation(item), out Tuple<Guid, string> guidNameTuple))
                {
                    exporter.GetOrAddFolder(guidNameTuple.Item1, guidNameTuple.Item2, isGenerated: true)
                        .Children.Add(getGuidFunc(item));
                }
            }
        }

        private Dictionary<AssetLocation, Tuple<Guid, string>> GetKnownFolderMapping()
        {
            return new Dictionary<AssetLocation, Tuple<Guid, string>>()
            {
                { AssetLocation.BuiltInPackage, new Tuple<Guid, string>(config.BuiltInPackagesFolderGuid, "Built In Packages") },
                { AssetLocation.PackageLibraryCache, new Tuple<Guid, string>(config.ImportedPackagesFolderGuid, "Imported Packages") },
                { AssetLocation.External, new Tuple<Guid, string>(config.ExternalPackagesFolderGuid, "External Packages") }
            };
        }


        private SolutionProject GetSolutionProjectEntryFrom(Project project, SolutionFileInfo solutionFileInfo)
        {
            SolutionProject toReturn = new SolutionProject(project.Guid, project.TypeGuid, project.Name, project.RelativePath, project.Sections, project.Dependencies);

            SetAdditionalProjectProperties(toReturn, project.Guid, solutionFileInfo, config =>
            {
                if (!toReturn.ConfigurationPlatformMapping.TryGetValue(config.SolutionConfig, out ProjectConfigurationPlatformMapping mapping))
                {
                    toReturn.ConfigurationPlatformMapping.Add(config.SolutionConfig, mapping = new ProjectConfigurationPlatformMapping());
                }
                mapping.ConfigurationPlatform = config.ProjectConfig;

                switch (config.Property)
                {
                    case "ActiveCfg":
                        break;
                    case "Build.0":
                        mapping.EnabledForBuild = true;
                        break;
                }
            });

            return toReturn;
        }

        private SolutionProject CreateSolutionProjectEntry(CSProjectInfo project, Uri relativePath, SolutionFileInfo solutionFileInfo)
        {
            IEnumerable<SolutionSection> sections = null;
            if (solutionFileInfo.Projects.TryGetValue(project.Guid, out Project parsedProject))
            {
                sections = parsedProject.Sections;
            }

            SolutionProject toReturn = new SolutionProject(project.Guid, SolutionProject.CSharpProjectTypeGuid, project.Name + ".msb4u", relativePath, sections, project.ProjectDependencies.Select(t => t.Dependency.Guid));

            foreach (CompilationPlatformInfo platform in AvailablePlatforms)
            {
                ProcessConfigurationPlatformMapping(toReturn, UnityConfigurationType.InEditor, platform, project.InEditorPlatforms);
                ProcessConfigurationPlatformMapping(toReturn, UnityConfigurationType.Player, platform, project.PlayerPlatforms);
            }

            SetAdditionalProjectProperties(toReturn, project.Guid, solutionFileInfo);

            return toReturn;
        }

        private void SetAdditionalProjectProperties(SolutionProject solutionProject, Guid projectGuid, SolutionFileInfo solutionFileInfo, Action<ProjectConfigurationEntry> knownConfigHandler = null)
        {
            // Parse additional properties
            if (solutionFileInfo.ProjectConfigurationEntires.TryGetValue(projectGuid, out List<ProjectConfigurationEntry> projectConfigurations))
            {
                foreach (ProjectConfigurationEntry config in projectConfigurations)
                {
                    if (config.Property != "Build.0" && config.Property != "ActiveCfg")
                    {
                        if (solutionProject.ConfigurationPlatformMapping.TryGetValue(config.SolutionConfig, out ProjectConfigurationPlatformMapping mapping))
                        {
                            mapping.AdditionalPropertyMappings[config.Property] = config.ProjectConfig;
                        }
                    }
                    else
                    {
                        knownConfigHandler?.Invoke(config);
                    }
                }
            }
        }

        private void ProcessConfigurationPlatformMapping(SolutionProject solutionProject, UnityConfigurationType configType, CompilationPlatformInfo platform, IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> enabledPlatforms)
        {
            ConfigurationPlatformPair configPair = new ConfigurationPlatformPair(configType, platform.Name);

            solutionProject.ConfigurationPlatformMapping.Add(configPair, new ProjectConfigurationPlatformMapping()
            {
                ConfigurationPlatform = configPair,
                EnabledForBuild = enabledPlatforms.ContainsKey(platform.BuildTarget)
            });
        }

        public void ExportProjects(IUnityProjectExporter unityProjectExporter, DirectoryInfo generatedProjectFolder)
        {
            foreach (KeyValuePair<string, CSProjectInfo> project in CSProjects)
            {
                bool isGenerated = project.Value.AssemblyDefinitionInfo.AssetLocation != AssetLocation.Package && project.Value.AssemblyDefinitionInfo.AssetLocation != AssetLocation.Project;

                ICSharpProjectExporter exporter = unityProjectExporter.CreateCSharpProjectExporter(MSBuildUnityProjectExporter.GetProjectPath(project.Value, generatedProjectFolder), generatedProjectFolder, isGenerated);
                exporter.DefaultPlatform = CurrentPlayerPlatform.Name;
                exporter.LanguageVersion = MSBuildTools.CSharpVersion;
                exporter.IsGenerated = isGenerated;
                foreach (CompilationPlatformInfo platform in AvailablePlatforms)
                {
                    exporter.SupportedPlatforms.Add(platform.Name);
                }

                project.Value.Export(exporter, generatedProjectFolder);
                exporter.Write();
            }
        }
        #endregion
    }
}
#endif