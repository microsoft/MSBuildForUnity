// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// This interface exposes teh APIs for exporting projects.
    /// </summary>
    public class TemplatedProjectExporter : IProjectExporter
    {
        private const string MSBuildFileSuffix = "msb4u";
        private static readonly Guid FolderProjectTypeGuid = Guid.Parse("2150E333-8FDC-42A3-9474-1A3956D46DE8");

        private readonly Dictionary<string, string> solutionProperties = new Dictionary<string, string>()
        {
            { "HideSolutionNode", "FALSE" }
        };

        private readonly DirectoryInfo generatedOutputFolder;

        private readonly FileTemplate projectFileTemplate;
        private readonly FileTemplate generatedProjectFileTemplate;
        private readonly FileTemplate propsFileTemplate;
        private readonly FileTemplate targetsFileTemplate;

        private readonly FileTemplate solutionFileTemplate;
        private readonly FileTemplate msbuildForUnityCommonTemplate;

        private readonly FileTemplate dependenciesProjectTemplate;
        private readonly FileTemplate dependenciesPropsTemplate;
        private readonly FileTemplate dependenciesTargetsTemplate;

        /// <summary>
        /// Creates a new instance of the template driven <see cref="IProjectExporter"/>.
        /// </summary>
        /// <param name="generatedOutputFolder">The output folder for the projects and props.</param>
        /// <param name="solutionFileTemplatePath">The path to the solution template.</param>
        /// <param name="projectFileTemplatePath">The path to the C# project file template.</param>
        /// <param name="projectPropsFileTemplatePath">The path to the props file template.</param>
        /// <param name="projectTargetsFileTemplatePath">The path to the targets file template.</param>
        /// <param name="generatedProjectFileTemplatePath">The path to the generated project file that won't be checked-in.</param>
        /// <param name="msbuildForUnityCommonTemplatePath">Path to the common props file that is quick generated.</param>
        /// <param name="dependenciesProjectTemplatePath">Path to the dependencies project template file.</param>
        /// <param name="dependenciesPropsTemplatePath">Path to the dependencies props template file.</param>
        /// <param name="dependenciesTargetsTemplatePath">Path to the dependencies targets template file.</param>
        public TemplatedProjectExporter(DirectoryInfo generatedOutputFolder, FileInfo solutionFileTemplatePath, FileInfo projectFileTemplatePath, FileInfo generatedProjectFileTemplatePath, FileInfo projectPropsFileTemplatePath, FileInfo projectTargetsFileTemplatePath, FileInfo msbuildForUnityCommonTemplatePath, FileInfo dependenciesProjectTemplatePath, FileInfo dependenciesPropsTemplatePath, FileInfo dependenciesTargetsTemplatePath)
        {
            this.generatedOutputFolder = generatedOutputFolder;

            FileTemplate.TryParseTemplate(projectFileTemplatePath, out projectFileTemplate);
            FileTemplate.TryParseTemplate(generatedProjectFileTemplatePath, out generatedProjectFileTemplate);
            FileTemplate.TryParseTemplate(projectPropsFileTemplatePath, out propsFileTemplate);
            FileTemplate.TryParseTemplate(projectTargetsFileTemplatePath, out targetsFileTemplate);

            FileTemplate.TryParseTemplate(solutionFileTemplatePath, out solutionFileTemplate);
            FileTemplate.TryParseTemplate(msbuildForUnityCommonTemplatePath, out msbuildForUnityCommonTemplate);

            FileTemplate.TryParseTemplate(dependenciesProjectTemplatePath, out dependenciesProjectTemplate);
            FileTemplate.TryParseTemplate(dependenciesPropsTemplatePath, out dependenciesPropsTemplate);
            FileTemplate.TryParseTemplate(dependenciesTargetsTemplatePath, out dependenciesTargetsTemplate);
        }

        private string GetProjectFilePath(DirectoryInfo directory, CSProjectInfo projectInfo)
        {
            return GetProjectFilePath(directory.FullName, projectInfo.Name);
        }

        private string GetProjectFilePath(string directory, string projectName)
        {
            return Path.Combine(directory, $"{projectName}.{MSBuildFileSuffix}.csproj");
        }

        ///<inherit-doc/>
        public FileInfo GetProjectPath(CSProjectInfo projectInfo)
        {
            switch (projectInfo.AssemblyDefinitionInfo.AssetLocation)
            {
                case AssetLocation.BuiltInPackage:
                case AssetLocation.External:
                case AssetLocation.PackageLibraryCache:
                    return new FileInfo(GetProjectFilePath(generatedOutputFolder, projectInfo));
                case AssetLocation.Project:
                case AssetLocation.Package:
                    return new FileInfo(GetProjectFilePath(projectInfo.AssemblyDefinitionInfo.Directory, projectInfo));
                default:
                    throw new InvalidOperationException("The project's assembly definition file is in an unknown location.");
            }
        }

        public string GetSolutionFilePath(UnityProjectInfo unityProjectInfo)
        {
            return Path.Combine(Utilities.AssetPath, $"{unityProjectInfo.UnityProjectName}.{MSBuildFileSuffix}.sln");
        }

        ///<inherit-doc/>
        public void ExportProject(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            string projectPath = GetProjectPath(projectInfo).FullName;

            bool generatedProject;
            switch (projectInfo.AssemblyDefinitionInfo.AssetLocation)
            {
                case AssetLocation.BuiltInPackage:
                case AssetLocation.External:
                case AssetLocation.PackageLibraryCache:
                    generatedProject = true;
                    break;
                case AssetLocation.Project:
                case AssetLocation.Package:
                    generatedProject = false;
                    break;
                default:
                    throw new InvalidOperationException("The project's assembly definition file is in an unknown location.");
            }

            if (!TryExportPropsFile(unityProjectInfo, projectInfo))
            {
                Debug.LogError($"Error exporting the generated props file for {projectInfo.Name}");
                return;
            }

            if (!TryExportTargetsFile(unityProjectInfo, projectInfo))
            {
                Debug.LogError($"Error exporting the generated targets file for {projectInfo.Name}");
                return;
            }

            if (generatedProject)
            {
                generatedProjectFileTemplate.Write(projectPath, generatedProjectFileTemplate.Root.CreateReplacementSet());
                File.SetAttributes(projectPath, FileAttributes.ReadOnly);
            }
            else if (!File.Exists(projectPath))
            {
                projectFileTemplate.Write(projectPath, projectFileTemplate.Root.CreateReplacementSet());
            }
        }

        private bool TryExportPropsFile(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            ITemplatePart rootTemplatePart = propsFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();

            ITemplatePart projectReferenceSetTemplatePart = rootTemplatePart.Templates["PROJECT_REFERENCE_SET"];
            ITemplatePart sourceExcludeTemplatePart = rootTemplatePart.Templates["SOURCE_EXCLUDE"];

            string projectPath = GetProjectFilePath(generatedOutputFolder, projectInfo);

            foreach (AssemblyDefinitionInfo nestedAsmdef in projectInfo.AssemblyDefinitionInfo.NestedAssemblyDefinitionFiles)
            {
                TemplateReplacementSet replacementSet = sourceExcludeTemplatePart.CreateReplacementSet(rootReplacementSet);
                sourceExcludeTemplatePart.Tokens["EXCLUDE_DIRECTORY_PATH"].AssignValue(replacementSet, nestedAsmdef.Directory.FullName);
            }

            HashSet<string> inEditorSearchPaths = new HashSet<string>(), playerSearchPaths = new HashSet<string>();
            CreateProjectReferencesSet(projectInfo, projectReferenceSetTemplatePart, rootReplacementSet, inEditorSearchPaths, true);
            CreateProjectReferencesSet(projectInfo, projectReferenceSetTemplatePart, rootReplacementSet, playerSearchPaths, false);

            rootTemplatePart.Tokens["PROJECT_GUID"].AssignValue(rootReplacementSet, projectInfo.Guid.ToString());
            rootTemplatePart.Tokens["ALLOW_UNSAFE"].AssignValue(rootReplacementSet, projectInfo.AssemblyDefinitionInfo.allowUnsafeCode.ToString());
            rootTemplatePart.Tokens["LANGUAGE_VERSION"].AssignValue(rootReplacementSet, MSBuildTools.CSharpVersion);
            rootTemplatePart.Tokens["DEVELOPMENT_BUILD"].AssignValue(rootReplacementSet, "false");
            rootTemplatePart.Tokens["IS_EDITOR_ONLY_TARGET"].AssignValue(rootReplacementSet, (projectInfo.ProjectType == ProjectType.EditorAsmDef || projectInfo.ProjectType == ProjectType.PredefinedEditorAssembly).ToString());
            rootTemplatePart.Tokens["UNITY_EDITOR_INSTALL_FOLDER"].AssignValue(rootReplacementSet, Path.GetDirectoryName(EditorApplication.applicationPath) + "\\");
            rootTemplatePart.Tokens["PROJECT_NAME"].AssignValue(rootReplacementSet, projectInfo.Name);
            rootTemplatePart.Tokens["DEFAULT_PLATFORM"].AssignValue(rootReplacementSet, unityProjectInfo.AvailablePlatforms.First(t => BuildPipeline.GetBuildTargetGroup(t.BuildTarget) == BuildTargetGroup.Standalone).Name);
            rootTemplatePart.Tokens["SUPPORTED_PLATFORMS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", unityProjectInfo.AvailablePlatforms.Select(t => t.Name)));
            rootTemplatePart.Tokens["INEDITOR_ASSEMBLY_SEARCH_PATHS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", inEditorSearchPaths));
            rootTemplatePart.Tokens["PLAYER_ASSEMBLY_SEARCH_PATHS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", playerSearchPaths));
            rootTemplatePart.Tokens["PLATFORM_PROPS_FOLDER_PATH"].AssignValue(rootReplacementSet, generatedOutputFolder.FullName);
            rootTemplatePart.Tokens["PROJECT_DIRECTORY_PATH"].AssignValue(rootReplacementSet, projectInfo.AssemblyDefinitionInfo.Directory.FullName);

            string propsFilePath = projectPath.Replace("csproj", "g.props");
            propsFileTemplate.Write(propsFilePath, rootReplacementSet);
            File.SetAttributes(propsFilePath, FileAttributes.ReadOnly);
            return true;
        }

        private bool TryExportTargetsFile(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            string projectPath = GetProjectFilePath(generatedOutputFolder, projectInfo);

            ITemplatePart rootTemplatePart = targetsFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();
            ITemplatePart supportedPlatformBuildTemplate = rootTemplatePart.Templates["SUPPORTED_PLATFORM_BUILD_CONDITION"];

            PopulateSupportedPlatformBuildConditions(supportedPlatformBuildTemplate, rootReplacementSet, "InEditor", projectInfo.InEditorPlatforms);
            PopulateSupportedPlatformBuildConditions(supportedPlatformBuildTemplate, rootReplacementSet, "Player", projectInfo.PlayerPlatforms);

            string targetsFilePath = projectPath.Replace("csproj", "g.targets");
            targetsFileTemplate.Write(targetsFilePath, rootReplacementSet);
            File.SetAttributes(targetsFilePath, FileAttributes.ReadOnly);

            return true;
        }

        public void GenerateDirectoryPropsFile(UnityProjectInfo unityProjectInfo)
        {
            string outputPath = Path.Combine(Utilities.ProjectPath, "MSBuildForUnity.Common.props");

            ITemplatePart rootTemplate = msbuildForUnityCommonTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplate.CreateReplacementSet(null);

            rootTemplate.Tokens["GENERATED_OUTPUT_DIRECTORY"].AssignValue(rootReplacementSet, generatedOutputFolder.FullName);
            rootTemplate.Tokens["UNITY_PROJECT_ASSETS_PATH"].AssignValue(rootReplacementSet, Path.GetFullPath(Application.dataPath));

            rootTemplate.Tokens["CURRENT_UNITY_PLATFORM"].AssignValue(rootReplacementSet, unityProjectInfo.CurrentPlayerPlatform.Name);
            rootTemplate.Tokens["CURRENT_TARGET_FRAMEWORK"].AssignValue(rootReplacementSet, unityProjectInfo.CurrentPlayerPlatform.TargetFramework.AsMSBuildString());

            string[] versionParts = Application.unityVersion.Split('.');
            rootTemplate.Tokens["UNITY_MAJOR_VERSION"].AssignValue(rootReplacementSet, versionParts[0]);
            rootTemplate.Tokens["UNITY_MINOR_VERSION"].AssignValue(rootReplacementSet, versionParts[1]);

            msbuildForUnityCommonTemplate.Write(outputPath, rootReplacementSet);
        }

        ///<inherit-doc/>
        public void ExportSolution(UnityProjectInfo unityProjectInfo, MSBuildToolsConfig config)
        {
            string solutionFilePath = GetSolutionFilePath(unityProjectInfo);

            SolutionFileInfo solutionFileInfo = null;

            // Parse existing solution
            if (File.Exists(solutionFilePath))
            {
                TextSolutionFileParser.TryParseExistingSolutionFile(solutionFilePath, out solutionFileInfo);
            }

            ITemplatePart rootTemplatePart = solutionFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();

            // Remove known folders & projects
            if (solutionFileInfo != null)
            {
                solutionFileInfo.Projects.Remove(config.BuiltInPackagesFolderGuid);
                solutionFileInfo.Projects.Remove(config.ImportedPackagesFolderGuid);
                solutionFileInfo.Projects.Remove(config.ExternalPackagesFolderGuid);
            }

            List<Tuple<CSProjectInfo, Guid, string>> folderNestedItems = new List<Tuple<CSProjectInfo, Guid, string>>();
            List<CSProjectInfo> orderedProjects = new List<CSProjectInfo>();

            ProcessProjects(unityProjectInfo, config, solutionFileInfo, solutionFilePath, rootTemplatePart, rootReplacementSet, folderNestedItems, orderedProjects);

            HashSet<Guid> generatedItems = new HashSet<Guid>();

            ProcessSolutionFolders(rootTemplatePart, rootReplacementSet, folderNestedItems, generatedItems, solutionFileInfo);

            // Process Solution file configurations
            SortedDictionary<string, SortedSet<string>> configPlatformMap = GetSolutuonConfigurationPlatformMap(unityProjectInfo, solutionFileInfo);
            ProcessSolutionConfigurationPlatform(rootTemplatePart, rootReplacementSet, configPlatformMap);

            // Process Configurations Mappings
            ProcessConfigPlatformMappings(unityProjectInfo, config, rootTemplatePart, rootReplacementSet, orderedProjects, generatedItems, configPlatformMap, solutionFileInfo);

            // Write Solution Properties
            ITemplatePart solutionPropertiesTemplate = rootTemplatePart.Templates["SOLUTION_PROPERTIES"];
            ProcessPropertiesSet(solutionPropertiesTemplate, rootReplacementSet, solutionProperties, solutionFileInfo?.Properties);

            // Write Extensibility Globals
            Dictionary<string, string> extensibilityGlobals = new Dictionary<string, string>() { { "SolutionGuid", "{" + config.SolutionGuid.ToString().ToUpper() + "}" } };
            ITemplatePart extensibilityGlobalsTemplate = rootTemplatePart.Templates["EXTENSIBILITY_GLOBALS"];
            ProcessPropertiesSet(extensibilityGlobalsTemplate, rootReplacementSet, extensibilityGlobals, solutionFileInfo?.ExtensibilityGlobals);

            // Write Solution Notes
            ITemplatePart solutuonNotesTemplate = rootTemplatePart.Templates["SOLUTION_NOTES"];
            Dictionary<string, string> generatedDictionary = generatedItems.ToDictionary(t => "{" + t.ToString().ToUpper() + "}", t => "msb4u.generated");
            ProcessPropertiesSet(solutuonNotesTemplate, rootReplacementSet, generatedDictionary, solutionFileInfo?.SolutionNotes);

            // Write Extra Sections found in the read Solution
            ProcessExtraSolutionSections(rootTemplatePart, rootReplacementSet, solutionFileInfo);

            // Export projects
            foreach (CSProjectInfo project in unityProjectInfo.CSProjects.Values)
            {
                ExportProject(unityProjectInfo, project);
            }

            GenerateTopLevelDependenciesProject(unityProjectInfo, config.DependenciesProjectGuid);

            // Delete before we write to minimize chance of just deleting in case above fails
            if (File.Exists(solutionFilePath))
            {
                File.Delete(solutionFilePath);
            }

            solutionFileTemplate.Write(solutionFilePath, rootReplacementSet);
        }

        private void ProcessExtraSolutionSections(ITemplatePart rootTemplatePart, TemplateReplacementSet rootReplacementSet, SolutionFileInfo solutionFileInfo)
        {
            if (solutionFileInfo != null)
            {
                ITemplatePart extraSectionTemplate = rootTemplatePart.Templates["EXTRA_GLOBAL_SECTION"];

                foreach (SolutionFileSection<SolutionGlobalSectionType> section in solutionFileInfo.SolutionSections.Values)
                {
                    ProcessExtraSection(extraSectionTemplate, rootReplacementSet, section.Name, section.Type == SolutionGlobalSectionType.PreSolution ? "preSolution" : "postSolution", section.Lines);
                }
            }
        }

        private void ConfigurationTemplateReplace(ITemplatePart templatePart, TemplateReplacementSet replacementSet, Guid projectGuid, string slnConfiguration, string slnPlatform, string property, string projConfiguration, string projPlatform)
        {
            templatePart.Tokens["PROJECT_GUID"].AssignValue(replacementSet, projectGuid.ToString().ToUpper());
            templatePart.Tokens["SOLUTION_CONFIGURATION"].AssignValue(replacementSet, slnConfiguration);
            templatePart.Tokens["SOLUTION_PLATFORM"].AssignValue(replacementSet, slnPlatform);
            templatePart.Tokens["PROPERTY"].AssignValue(replacementSet, property);
            templatePart.Tokens["PROJECT_CONFIGURATION"].AssignValue(replacementSet, projConfiguration);
            templatePart.Tokens["PROJECT_PLATFORM"].AssignValue(replacementSet, projPlatform);
        }

        private class ProjectConfigurationMapping
        {
            private readonly Dictionary<ConfigPlatformPair, HashSet<string>> propertySet = new Dictionary<ConfigPlatformPair, HashSet<string>>();

            public SortedDictionary<ConfigPlatformPair, ConfigPlatformPair> Mappings { get; } = new SortedDictionary<ConfigPlatformPair, ConfigPlatformPair>(ConfigPlatformPair.Comparer.Instance);

            public HashSet<string> GetPropertySet(ConfigPlatformPair configPair)
            {
                if (!propertySet.TryGetValue(configPair, out HashSet<string> set))
                {
                    propertySet.Add(configPair, set = new HashSet<string>());
                }

                return set;
            }

            public void AddConfigurationMapping(ConfigPlatformPair configMapping, params string[] properties)
            {
                AddConfigurationMapping(configMapping, configMapping, properties);
            }

            public void AddConfigurationMapping(ConfigPlatformPair solutionMapping, ConfigPlatformPair projectMapping, params string[] properties)
            {
                Mappings[solutionMapping] = projectMapping;
                HashSet<string> set = GetPropertySet(solutionMapping);
                foreach (string property in properties)
                {
                    set.Add(property);
                }
            }
        }

        private void ProcessMappings(ITemplatePart configPlatformPropertyTemplate, TemplateReplacementSet rootReplacementSet, IEnumerable<Guid> projectOrdering, Dictionary<Guid, ProjectConfigurationMapping> projectConfigMapping)
        {
            foreach (Guid projectGuid in projectOrdering)
            {
                ProjectConfigurationMapping mapping = projectConfigMapping[projectGuid];

                foreach (KeyValuePair<ConfigPlatformPair, ConfigPlatformPair> configSet in mapping.Mappings)
                {
                    foreach (string property in mapping.GetPropertySet(configSet.Key))
                    {
                        TemplateReplacementSet configMappingReplacementSet = configPlatformPropertyTemplate.CreateReplacementSet(rootReplacementSet);
                        ConfigurationTemplateReplace(configPlatformPropertyTemplate, configMappingReplacementSet, projectGuid, configSet.Key.Configuration, configSet.Key.Platform, property, configSet.Value.Configuration, configSet.Value.Platform);
                    }
                }
            }
        }

        private void ProcessConfigPlatformMappings(UnityProjectInfo unityProjectInfo, MSBuildToolsConfig config, ITemplatePart rootTemplatePart, TemplateReplacementSet rootReplacementSet, List<CSProjectInfo> orderedProjects, HashSet<Guid> generatedItems, SortedDictionary<string, SortedSet<string>> configPlatformMap, SolutionFileInfo solutionFileInfo)
        {
            ITemplatePart configPlatformPropertyTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM_PROPERTY"];

            List<Guid> projectOrdering = new List<Guid>();
            Dictionary<Guid, ProjectConfigurationMapping> projectConfigMapping = new Dictionary<Guid, ProjectConfigurationMapping>();

            void ProcessDefaultMappings(ProjectConfigurationMapping mapping, Dictionary<string, List<string>> enabledMappings)
            {
                // Process defaults
                foreach (KeyValuePair<string, SortedSet<string>> pair in configPlatformMap)
                {
                    string projectConfig = pair.Key;
                    string defaultPlatform = null;
                    if (!enabledMappings.TryGetValue(projectConfig, out List<string> enabledPlatforms))
                    {
                        KeyValuePair<string, List<string>> enabledPair = enabledMappings.First();
                        projectConfig = enabledPair.Key;
                        defaultPlatform = enabledPair.Value[0];
                    }

                    foreach (string platform in pair.Value)
                    {
                        ConfigPlatformPair slnConfigPair = new ConfigPlatformPair(pair.Key, platform);
                        if (!mapping.Mappings.ContainsKey(slnConfigPair))
                        {
                            ConfigPlatformPair projectPair;
                            if (!(enabledPlatforms?.Contains(platform) ?? false))
                            {
                                projectPair = new ConfigPlatformPair(projectConfig, enabledPlatforms?[0] ?? defaultPlatform);
                            }
                            else
                            {
                                projectPair = new ConfigPlatformPair(projectConfig, platform);
                            }
                            mapping.Mappings[slnConfigPair] = projectPair;
                            HashSet<string> set = mapping.GetPropertySet(slnConfigPair);
                            set.Add("ActiveCfg");
                        }
                    }
                }
            }

            // Iterate over every project
            foreach (CSProjectInfo project in orderedProjects)
            {
                // Mark as generated item
                generatedItems.Add(project.Guid);

                // Add it to the ordering List for output
                projectOrdering.Add(project.Guid);

                // Create mapping container for the project
                ProjectConfigurationMapping mapping = new ProjectConfigurationMapping();
                projectConfigMapping.Add(project.Guid, mapping);

                void ProcessProjectPlatforms(string configuration, IEnumerable<string> platforms)
                {
                    foreach (string platform in platforms)
                    {
                        mapping.AddConfigurationMapping(new ConfigPlatformPair(configuration, platform), "ActiveCfg", "Build.0");
                    }
                }

                List<string> enabledInEditorPlatforms = project.InEditorPlatforms.Select(t => t.Value.Name).ToList();
                List<string> enabledPlayerPlatforms = project.PlayerPlatforms.Select(t => t.Value.Name).ToList();

                // Add InEditor and Player platform mappings that are enabled for build
                ProcessProjectPlatforms("InEditor", enabledInEditorPlatforms);
                ProcessProjectPlatforms("Player", enabledPlayerPlatforms);

                // Add all other known solution mappings, map to itself or allowed mappings
                ProcessDefaultMappings(mapping, new Dictionary<string, List<string>> { { "InEditor", enabledInEditorPlatforms }, { "Player", enabledPlayerPlatforms } });
            }

            // Process the Dependencies Project now
            generatedItems.Add(config.DependenciesProjectGuid);
            projectOrdering.Add(config.DependenciesProjectGuid);

            ProjectConfigurationMapping dependencyProjectMapping = new ProjectConfigurationMapping();
            projectConfigMapping.Add(config.DependenciesProjectGuid, dependencyProjectMapping);

            // Add default Release/Debug mappings
            dependencyProjectMapping.AddConfigurationMapping(new ConfigPlatformPair("Debug", "Any CPU"), "ActiveCfg", "Build.0");
            dependencyProjectMapping.AddConfigurationMapping(new ConfigPlatformPair("Release", "Any CPU"), "ActiveCfg", "Build.0");

            List<string> anyCpuPlatform = new List<string> { "Any CPU" };
            ProcessDefaultMappings(dependencyProjectMapping, new Dictionary<string, List<string>> { { "Debug", anyCpuPlatform }, { "Release", anyCpuPlatform } });

            // Process projects that we aren't generating, but were added to the solution file
            if (solutionFileInfo != null)
            {
                foreach (KeyValuePair<Guid, List<ProjectConfigurationEntry>> pair in solutionFileInfo.ProjectConfigurationEntires)
                {
                    if (solutionFileInfo.Projects.ContainsKey(pair.Key))
                    {
                        projectOrdering.Add(pair.Key);

                        ProjectConfigurationMapping mapping = new ProjectConfigurationMapping();
                        projectConfigMapping.Add(pair.Key, mapping);

                        foreach (ProjectConfigurationEntry item in pair.Value)
                        {
                            mapping.Mappings[item.SolutionConfig] = item.ProjectConfig;
                            mapping.GetPropertySet(item.SolutionConfig).Add(item.Property);
                        }
                    }
                }
            }

            ProcessMappings(configPlatformPropertyTemplate, rootReplacementSet, projectOrdering, projectConfigMapping);
        }

        private void ProcessPropertiesSet(ITemplatePart solutionPropertiesTemplate, TemplateReplacementSet rootReplacementSet, Dictionary<string, string> propertySet, Dictionary<string, string> existingPropertySet)
        {
            void WriteSolutionProperty(string key, string value)
            {
                TemplateReplacementSet replacementSet = solutionPropertiesTemplate.CreateReplacementSet(rootReplacementSet);
                solutionPropertiesTemplate.Tokens["PROPERTY_KEY"].AssignValue(replacementSet, key);
                solutionPropertiesTemplate.Tokens["PROPERTY_VALUE"].AssignValue(replacementSet, value);
            }

            foreach (KeyValuePair<string, string> prop in propertySet)
            {
                WriteSolutionProperty(prop.Key, prop.Value);
            }

            if (existingPropertySet != null)
            {
                foreach (KeyValuePair<string, string> prop in existingPropertySet)
                {
                    if (!propertySet.ContainsKey(prop.Key))
                    {
                        WriteSolutionProperty(prop.Key, prop.Value);
                    }
                }
            }
        }

        private SortedDictionary<string, SortedSet<string>> GetSolutuonConfigurationPlatformMap(UnityProjectInfo unityProjectInfo, SolutionFileInfo solutionFileInfo)
        {
            SortedDictionary<string, SortedSet<string>> configPlatformMap = new SortedDictionary<string, SortedSet<string>>();

            void AddPairToMap(string configuration, string platform)
            {
                if (!configPlatformMap.TryGetValue(configuration, out SortedSet<string> set))
                {
                    configPlatformMap[configuration] = set = new SortedSet<string>();
                }

                set.Add(platform);
            }

            foreach (CompilationPlatformInfo platform in unityProjectInfo.AvailablePlatforms)
            {
                AddPairToMap("InEditor", platform.Name);
                AddPairToMap("Player", platform.Name);
            }

            if (solutionFileInfo != null)
            {
                foreach (ConfigPlatformPair item in solutionFileInfo.ConfigPlatformPairs)
                {
                    AddPairToMap(item.Configuration, item.Platform);
                }
            }

            return configPlatformMap;
        }

        private void ProcessSolutionConfigurationPlatform(ITemplatePart rootTemplatePart, TemplateReplacementSet rootReplacementSet, SortedDictionary<string, SortedSet<string>> configPlatformMap)
        {
            ITemplatePart configPlatformTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM"];

            ITemplateToken configPlatform_ConfigurationToken = configPlatformTemplate.Tokens["CONFIGURATION"];
            ITemplateToken configPlatform_PlatformToken = configPlatformTemplate.Tokens["PLATFORM"];

            void WriteConfigPlatformMapping(string configValue, string platformValue)
            {
                TemplateReplacementSet replacementSet = configPlatformTemplate.CreateReplacementSet(rootReplacementSet);
                configPlatform_ConfigurationToken.AssignValue(replacementSet, configValue);
                configPlatform_PlatformToken.AssignValue(replacementSet, platformValue);
            }

            foreach (KeyValuePair<string, SortedSet<string>> setPair in configPlatformMap)
            {
                foreach (string platform in setPair.Value)
                {
                    WriteConfigPlatformMapping(setPair.Key, platform);
                }
            }
        }

        private void ProcessProjects(UnityProjectInfo unityProjectInfo, MSBuildToolsConfig config, SolutionFileInfo solutionFileInfo, string solutionFilePath, ITemplatePart rootTemplatePart, TemplateReplacementSet rootReplacementSet, List<Tuple<CSProjectInfo, Guid, string>> folderNestedItems, List<CSProjectInfo> orderedProjects)
        {
            ITemplatePart projectTemplate = rootTemplatePart.Templates["PROJECT"];

            CSProjectInfo[] unorderedProjects = unityProjectInfo.CSProjects.Select(t => t.Value).ToArray();

            while (orderedProjects.Count < unorderedProjects.Length)
            {
                bool oneRemoved = false;
                for (int i = 0; i < unorderedProjects.Length; i++)
                {
                    if (unorderedProjects[i] == null)
                    {
                        continue;
                    }

                    if (unorderedProjects[i].ProjectDependencies.Count == 0 || unorderedProjects[i].ProjectDependencies.All(t => orderedProjects.Contains(t.Dependency)))
                    {
                        orderedProjects.Add(unorderedProjects[i]);

                        unorderedProjects[i] = null;
                        oneRemoved = true;
                    }
                }

                if (!oneRemoved)
                {
                    Debug.LogError($"Possible circular dependency.");
                    break;
                }
            }

            foreach (CSProjectInfo project in orderedProjects)
            {
                IEnumerable<SolutionFileSection<SolutionProjecSectionType>> extraSections = null;
                if (solutionFileInfo != null)
                {
                    if (solutionFileInfo.Projects.TryGetValue(project.Guid, out Project existingProject))
                    {
                        solutionFileInfo.Projects.Remove(project.Guid);
                        extraSections = existingProject.Sections.Values;
                    }
                }

                TemplateReplacementSet replacementSet = projectTemplate.CreateReplacementSet(rootReplacementSet);
                ProcessProjectEntry(project.Name + ".msb4u", Utilities.GetRelativePath(Path.GetDirectoryName(solutionFilePath), GetProjectPath(project).FullName), project.Guid, project.ProjectDependencies.Select(t => t.Dependency.Guid).ToList(), projectTemplate, replacementSet, extraSections);

                switch (project.AssemblyDefinitionInfo.AssetLocation)
                {
                    case AssetLocation.BuiltInPackage:
                        folderNestedItems.Add(new Tuple<CSProjectInfo, Guid, string>(project, config.BuiltInPackagesFolderGuid, "Built In Packages"));
                        break;
                    case AssetLocation.PackageLibraryCache:
                        folderNestedItems.Add(new Tuple<CSProjectInfo, Guid, string>(project, config.ImportedPackagesFolderGuid, "Imported Packages"));
                        break;
                    case AssetLocation.External:
                        folderNestedItems.Add(new Tuple<CSProjectInfo, Guid, string>(project, config.ExternalPackagesFolderGuid, "External Packages"));
                        break;
                    default: break;
                }
            }

            // Add the "Dependencies" project
            {
                string dependencyRelativePath = Utilities.GetRelativePath(Path.GetDirectoryName(solutionFilePath), GetProjectFilePath(Utilities.AssetPath, "Dependencies"));
                IEnumerable<SolutionFileSection<SolutionProjecSectionType>> extraSections = null;

                if (solutionFileInfo != null)
                {
                    if (solutionFileInfo.Projects.TryGetValue(config.DependenciesProjectGuid, out Project existingProject))
                    {
                        solutionFileInfo.Projects.Remove(config.DependenciesProjectGuid);
                        extraSections = existingProject.Sections.Values;
                    }
                }
                ProcessProjectEntry("Dependencies.msb4u", dependencyRelativePath, config.DependenciesProjectGuid, null, projectTemplate, projectTemplate.CreateReplacementSet(rootReplacementSet), extraSections);
            }

            if (solutionFileInfo != null)
            {
                foreach (Guid guid in solutionFileInfo.MSB4UGeneratedItems)
                {
                    solutionFileInfo.Projects.Remove(guid);
                }

                // Process existing projects
                foreach (Project project in solutionFileInfo.Projects.Values)
                {
                    // Don't process folders or what we previously thought were generated projects
                    if (project.TypeGuid != FolderProjectTypeGuid)
                    {
                        TemplateReplacementSet replacementSet = projectTemplate.CreateReplacementSet(rootReplacementSet);
                        ProcessProjectEntry(project.Name, project.RelativePath, project.Guid, project.Dependencies, projectTemplate, replacementSet, project.Sections.Values);
                    }
                }
            }
        }

        private void GenerateTopLevelDependenciesProject(UnityProjectInfo unityProjectInfo, Guid dependenciesProjectGuid)
        {
            string projectPath = GetProjectFilePath(Utilities.AssetPath, "Dependencies");
            string propsPath = GetProjectFilePath(generatedOutputFolder.FullName, "Dependencies").Replace(".csproj", ".g.props");
            string targetsPath = GetProjectFilePath(generatedOutputFolder.FullName, "Dependencies").Replace(".csproj", ".g.targets");

            ITemplatePart propsFileTemplate = dependenciesPropsTemplate.Root;
            ITemplatePart projectReferenceTemplate = propsFileTemplate.Templates["PROJECT_REFERENCE"];

            TemplateReplacementSet replacementSet = propsFileTemplate.CreateReplacementSet();

            propsFileTemplate.Tokens["PROJECT_GUID"].AssignValue(replacementSet, dependenciesProjectGuid.ToString().ToUpper());

            // We use this to emulate the platform support for all 
            Dictionary<BuildTarget, CompilationPlatformInfo> allPlatforms = unityProjectInfo.AvailablePlatforms.ToDictionary(t => t.BuildTarget, t => t);
            foreach (CSProjectInfo projectInfo in unityProjectInfo.CSProjects.Values)
            {
                List<string> platformConditions = GetPlatformConditions(allPlatforms, projectInfo.InEditorPlatforms.Keys);
                ProcessProjectDependency(replacementSet, projectReferenceTemplate, projectInfo, platformConditions);
            }

            dependenciesPropsTemplate.Write(propsPath, replacementSet);

            ITemplatePart targetsFileTemplate = dependenciesTargetsTemplate.Root;

            dependenciesTargetsTemplate.Write(targetsPath, propsFileTemplate.CreateReplacementSet());

            if (!File.Exists(projectPath))
            {
                dependenciesProjectTemplate.Write(projectPath, dependenciesProjectTemplate.Root.CreateReplacementSet());
            }
        }

        private void ProcessSolutionFolders(ITemplatePart rootTemplatePart, TemplateReplacementSet parentReplacementSet, List<Tuple<CSProjectInfo, Guid, string>> folderNestedItems, HashSet<Guid> generatedItems, SolutionFileInfo solutionFileInfo)
        {
            ITemplatePart folderTemplate = rootTemplatePart.Templates["FOLDER"];
            ITemplatePart folderNestedProjectsTemplate = rootTemplatePart.Templates["FOLDER_NESTED_PROJECTS"];

            void AddFolder(string name, string guid)
            {
                TemplateReplacementSet replacementSet = folderTemplate.CreateReplacementSet(parentReplacementSet);
                folderTemplate.Tokens["FOLDER_NAME"].AssignValue(replacementSet, name);
                folderTemplate.Tokens["FOLDER_GUID"].AssignValue(replacementSet, guid);
            }

            void AddNestedMapping(string parentGuid, string childGuid)
            {
                TemplateReplacementSet nestedReplacementSet = folderNestedProjectsTemplate.CreateReplacementSet(parentReplacementSet);
                folderNestedProjectsTemplate.Tokens["FOLDER_GUID"].AssignValue(nestedReplacementSet, parentGuid);
                folderNestedProjectsTemplate.Tokens["CHILD_GUID"].AssignValue(nestedReplacementSet, childGuid);
            }

            HashSet<string> addedFolders = new HashSet<string>();

            foreach (Tuple<CSProjectInfo, Guid, string> tuple in folderNestedItems)
            {
                string guidStr = tuple.Item2.ToString().ToUpper();

                generatedItems.Add(tuple.Item2);

                if (addedFolders.Add(guidStr))
                {
                    AddFolder(tuple.Item3, guidStr);
                }

                Guid childGuid = tuple.Item1.Guid;
                AddNestedMapping(guidStr, childGuid.ToString().ToUpper());
                if (solutionFileInfo != null)
                {
                    solutionFileInfo.ChildToParentNestedMappings.Remove(childGuid);
                }
            }

            if (solutionFileInfo != null)
            {
                foreach (Project project in solutionFileInfo.Projects.Values)
                {
                    if (project.TypeGuid == FolderProjectTypeGuid)
                    {
                        string guidString = project.Guid.ToString().ToUpper();
                        if (addedFolders.Add(guidString))
                        {
                            AddFolder(project.Name, guidString);
                        }
                    }
                }

                foreach (KeyValuePair<Guid, Guid> mapping in solutionFileInfo.ChildToParentNestedMappings)
                {
                    AddNestedMapping(mapping.Value.ToString().ToUpper(), mapping.Key.ToString().ToUpper());
                }
            }
        }

        private void PopulateSupportedPlatformBuildConditions(ITemplatePart templatePart, TemplateReplacementSet parentReplacementSet, string configuration, IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> platforms)
        {
            foreach (KeyValuePair<BuildTarget, CompilationPlatformInfo> platform in platforms)
            {
                TemplateReplacementSet replacementSet = templatePart.CreateReplacementSet(parentReplacementSet);
                templatePart.Tokens["SUPPORTED_CONFIGURATION"].AssignValue(replacementSet, configuration);
                templatePart.Tokens["SUPPORTED_PLATFORM"].AssignValue(replacementSet, platform.Value.Name);
            }
        }

        private void CreateProjectReferencesSet(CSProjectInfo projectInfo, ITemplatePart templatePart, TemplateReplacementSet parentReplacementSet, HashSet<string> additionalSearchPaths, bool inEditor)
        {
            TemplateReplacementSet templateReplacementSet = templatePart.CreateReplacementSet(parentReplacementSet);
            ITemplatePart projectReferenceTemplatePart = templatePart.Templates["PROJECT_REFERENCE"];
            ITemplatePart pluginReferenceTemplatePart = templatePart.Templates["PLUGIN_REFERENCE"];

            foreach (CSProjectDependency<CSProjectInfo> dependency in projectInfo.ProjectDependencies)
            {
                List<string> platformConditions = GetPlatformConditions(inEditor ? projectInfo.InEditorPlatforms : projectInfo.PlayerPlatforms, inEditor ? dependency.InEditorSupportedPlatforms : dependency.PlayerSupportedPlatforms);
                ProcessProjectDependency(templateReplacementSet, projectReferenceTemplatePart, dependency.Dependency, platformConditions);
            }

            foreach (CSProjectDependency<PluginAssemblyInfo> dependency in projectInfo.PluginDependencies)
            {
                if (dependency.Dependency.Type == PluginType.Native)
                {
                    continue;
                }
                List<string> platformConditions = GetPlatformConditions(inEditor ? projectInfo.InEditorPlatforms : projectInfo.PlayerPlatforms, inEditor ? dependency.InEditorSupportedPlatforms : dependency.PlayerSupportedPlatforms);

                TemplateReplacementSet replacementSet = pluginReferenceTemplatePart.CreateReplacementSet(templateReplacementSet);
                pluginReferenceTemplatePart.Tokens["REFERENCE"].AssignValue(replacementSet, dependency.Dependency.Name);
                pluginReferenceTemplatePart.Tokens["HINT_PATH"].AssignValue(replacementSet, dependency.Dependency.ReferencePath.LocalPath);
                pluginReferenceTemplatePart.Tokens["CONDITION"].AssignValue(replacementSet, platformConditions.Count == 0 ? "false" : string.Join(" OR ", platformConditions));

                additionalSearchPaths.Add(Path.GetDirectoryName(dependency.Dependency.ReferencePath.LocalPath));
            }

            templatePart.Tokens["REFERENCE_CONFIGURATION"].AssignValue(templateReplacementSet, inEditor ? "InEditor" : "Player");
        }

        private void ProcessProjectDependency(TemplateReplacementSet parentReplacementSet, ITemplatePart projectReferenceTemplatePart, CSProjectInfo dependency, List<string> platformConditions)
        {
            string projectPath = GetProjectPath(dependency).FullName;
            TemplateReplacementSet replacementSet = projectReferenceTemplatePart.CreateReplacementSet(parentReplacementSet);
            projectReferenceTemplatePart.Tokens["REFERENCE"].AssignValue(replacementSet, projectPath);
            projectReferenceTemplatePart.Tokens["CONDITION"].AssignValue(replacementSet, platformConditions.Count == 0 ? "false" : string.Join(" OR ", platformConditions));
        }

        private List<string> GetPlatformConditions(IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> platforms, IEnumerable<BuildTarget> dependencyPlatforms)
        {
            List<string> toReturn = new List<string>();

            foreach (BuildTarget platform in dependencyPlatforms)
            {
                if (platforms.TryGetValue(platform, out CompilationPlatformInfo platformInfo))
                {
                    string platformName = platformInfo.Name;
                    toReturn.Add($"'$(UnityPlatform)' == '{platformName}'");
                }
            }

            return toReturn;
        }

        private void ProcessProjectEntry(string projectName, string projectRelativePath, Guid projectGuid, IReadOnlyCollection<Guid> projectDependencies, ITemplatePart templatePart, TemplateReplacementSet projectReplacementSet, IEnumerable<SolutionFileSection<SolutionProjecSectionType>> extraSections = null)
        {
            templatePart.Tokens["PROJECT_NAME"].AssignValue(projectReplacementSet, projectName);
            templatePart.Tokens["PROJECT_RELATIVE_PATH"].AssignValue(projectReplacementSet, projectRelativePath);
            templatePart.Tokens["PROJECT_GUID"].AssignValue(projectReplacementSet, projectGuid.ToString().ToUpper());

            ITemplatePart projectSectionTemplate = templatePart.Templates["PROJECT_SECTION"];
            ITemplatePart dependencyTemplate = projectSectionTemplate.Templates["PROJECT_DEPENDENCY"];

            if (projectDependencies != null && projectDependencies.Count > 0)
            {
                TemplateReplacementSet projectSectionReplacementSet = projectSectionTemplate.CreateReplacementSet(projectReplacementSet);
                foreach (Guid dependencyGuid in projectDependencies)
                {
                    TemplateReplacementSet set = dependencyTemplate.CreateReplacementSet(projectSectionReplacementSet);
                    dependencyTemplate.Tokens["DEPENDENCY_GUID"].AssignValue(set, dependencyGuid.ToString().ToUpper());
                }
            }

            ITemplatePart extraProjectSectionTemplate = templatePart.Templates["EXTRA_PROJECT_SECTION"];

            if (extraSections != null)
            {
                foreach (SolutionFileSection<SolutionProjecSectionType> section in extraSections)
                {
                    ProcessExtraSection(extraProjectSectionTemplate, projectReplacementSet, section.Name, section.Type == SolutionProjecSectionType.PreProject ? "preProject" : "postProject", section.Lines);
                }
            }
        }

        private void ProcessExtraSection(ITemplatePart extraSectionTemplate, TemplateReplacementSet parentReplacementSet, string sectionName, string sectionType, IEnumerable<string> sectionLines)
        {
            ITemplatePart extraSectionLineTemplate = extraSectionTemplate.Templates["EXTRA_SECTION_LINE"];

            TemplateReplacementSet sectionReplacementSet = extraSectionTemplate.CreateReplacementSet(parentReplacementSet);
            extraSectionTemplate.Tokens["SECTION_NAME"].AssignValue(sectionReplacementSet, sectionName);
            extraSectionTemplate.Tokens["PRE_POST_SOLUTION"].AssignValue(sectionReplacementSet, sectionType);

            foreach (string line in sectionLines)
            {
                TemplateReplacementSet lineReplacementSet = extraSectionLineTemplate.CreateReplacementSet(sectionReplacementSet);
                extraSectionLineTemplate.Tokens["SECTION_LINE"].AssignValue(lineReplacementSet, line);
            }
        }

        public void ExportPlatformPropsFile(CompilationPlatformInfo platform, bool inEditorConfiguration)
        {
            string configuration = inEditorConfiguration ? "InEditor" : "Player";

            if (!FileTemplate.TryParseTemplate(TemplateFiles.Instance.GetTemplateFilePathForPlatform(platform.Name, configuration, platform.ScriptingBackend), out FileTemplate fileTemplate))
            {
                throw new InvalidOperationException("Failed to parse template file for common props.");
            }

            ITemplatePart rootPart = fileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootPart.CreateReplacementSet();

            if (inEditorConfiguration)
            {
                ProcessPlatformTemplate(rootPart, rootReplacementSet, platform.Name, configuration, platform.BuildTarget, platform.TargetFramework,
                    platform.CommonPlatformReferences.Concat(platform.AdditionalInEditorReferences),
                    platform.CommonPlatformDefines.Concat(platform.AdditionalInEditorDefines));
            }
            else
            {
                ProcessPlatformTemplate(rootPart, rootReplacementSet, platform.Name, configuration, platform.BuildTarget, platform.TargetFramework,
                    platform.CommonPlatformReferences.Concat(platform.AdditionalPlayerReferences),
                    platform.CommonPlatformDefines.Concat(platform.AdditionalPlayerDefines));
            }

            fileTemplate.Write(Path.Combine(generatedOutputFolder.FullName, $"{platform.Name}.{configuration}.props"), rootReplacementSet);
        }

        private void ProcessPlatformTemplate(ITemplatePart rootPart, TemplateReplacementSet rootReplacementSet, string platformName, string configuration, BuildTarget buildTarget, TargetFramework targetFramework, IEnumerable<string> references, IEnumerable<string> defines, params HashSet<string>[] priorToCheck)
        {
            ProcessReferences(buildTarget, references, out HashSet<string> platformAssemblySearchPaths, out HashSet<string> platformAssemblyReferencePaths, priorToCheck);

            string minUWPPlatform = EditorUserBuildSettings.wsaMinUWPSDK;
            if (string.IsNullOrWhiteSpace(minUWPPlatform) || new Version(minUWPPlatform) < MSBuildTools.DefaultMinUWPSDK)
            {
                minUWPPlatform = MSBuildTools.DefaultMinUWPSDK.ToString();
            }

            // This is a try replace because some may hardcode this value
            rootPart.TryReplaceToken("TARGET_FRAMEWORK", rootReplacementSet, targetFramework.AsMSBuildString());

            rootPart.Tokens["PLATFORM_COMMON_DEFINE_CONSTANTS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", defines));
            rootPart.Tokens["PLATFORM_COMMON_ASSEMBLY_SEARCH_PATHS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", platformAssemblySearchPaths));

            // These are UWP specific, but they will be no-op if not needed
            if (buildTarget == BuildTarget.WSAPlayer && configuration == "Player")
            {
                string targetUWPPlatform = EditorUserBuildSettings.wsaUWPSDK;
                if (string.IsNullOrWhiteSpace(targetUWPPlatform))
                {
                    targetUWPPlatform = Utilities.GetUWPSDKs().Max().ToString(4);
                }
                rootPart.TryReplaceToken("UWP_TARGET_PLATFORM_VERSION", rootReplacementSet, targetUWPPlatform);
                rootPart.TryReplaceToken("UWP_MIN_PLATFORM_VERSION", rootReplacementSet, minUWPPlatform);
            }

            ITemplatePart platformCommonReferencePart = rootPart.Templates["PLATFORM_COMMON_REFERENCE"];
            foreach (string reference in platformAssemblyReferencePaths)
            {
                TemplateReplacementSet replacementSet = platformCommonReferencePart.CreateReplacementSet(rootReplacementSet);
                platformCommonReferencePart.Tokens["REFERENCE"].AssignValue(replacementSet, Path.GetFileNameWithoutExtension(reference));
                platformCommonReferencePart.Tokens["HINT_PATH"].AssignValue(replacementSet, reference);
            }
        }

        private void ProcessReferences(BuildTarget buildTarget, IEnumerable<string> references, out HashSet<string> searchPaths, out HashSet<string> referenceNames, params HashSet<string>[] priorToCheck)
        {
            searchPaths = new HashSet<string>();
            referenceNames = new HashSet<string>();

            foreach (string reference in references)
            {
                string directory = Path.GetDirectoryName(reference);
                string fileName = Path.GetFileName(reference);
                if (!priorToCheck.Any(t => t.Contains(directory))) // Don't add duplicates
                {
                    searchPaths.Add(directory);
                }

                if (!referenceNames.Add(reference))
                {
                    Debug.LogError($"Duplicate assembly reference found for platform '{buildTarget}' - {reference} ignoring.");
                }
            }
        }
    }
}
#endif
