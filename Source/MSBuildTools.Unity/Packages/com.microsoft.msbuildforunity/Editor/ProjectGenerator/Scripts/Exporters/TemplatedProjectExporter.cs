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
            rootTemplatePart.Tokens["DEFAULT_PLATFORM"].AssignValue(rootReplacementSet, unityProjectInfo.AvailablePlatforms.First(t => t.BuildTarget == BuildTarget.StandaloneWindows).Name);
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
        public void ExportSolution(UnityProjectInfo unityProjectInfo)
        {
            string solutionFilePath = GetSolutionFilePath(unityProjectInfo);

            if (File.Exists(solutionFilePath))
            {
                File.Delete(solutionFilePath);
            }
            ITemplatePart rootTemplatePart = solutionFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();

            ITemplatePart projectTemplate = rootTemplatePart.Templates["PROJECT"];
            ITemplatePart folderTemplate = rootTemplatePart.Templates["FOLDER"];
            ITemplatePart configPlatformTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM"];
            ITemplatePart configPlatformMappingTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM_MAPPING"];
            ITemplatePart configPlatformEnabledTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM_ENABLED"];
            ITemplatePart folderNestedProjectsTemplate = rootTemplatePart.Templates["FOLDER_NESTED_PROJECTS"];

            CSProjectInfo[] unorderedProjects = unityProjectInfo.CSProjects.Select(t => t.Value).ToArray();
            List<CSProjectInfo> orderedProjects = new List<CSProjectInfo>();

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

            List<CSProjectInfo> builtinPackages = new List<CSProjectInfo>();
            List<CSProjectInfo> importedPacakges = new List<CSProjectInfo>();
            List<CSProjectInfo> externalPackages = new List<CSProjectInfo>();
            foreach (CSProjectInfo project in orderedProjects)
            {
                TemplateReplacementSet replacementSet = projectTemplate.CreateReplacementSet(rootReplacementSet);
                ProcessProjectEntry(project.Name, GetProjectPath(project).FullName, project.Guid, project.ProjectDependencies, projectTemplate, replacementSet);

                switch (project.AssemblyDefinitionInfo.AssetLocation)
                {
                    case AssetLocation.BuiltInPackage:
                        builtinPackages.Add(project);
                        break;
                    case AssetLocation.PackageLibraryCache:
                        importedPacakges.Add(project);
                        break;
                    case AssetLocation.External:
                        externalPackages.Add(project);
                        break;
                    default: break;
                }
            }

            // Add the "Dependencies" project
            ProcessProjectEntry("Dependencies", GetProjectFilePath(Utilities.AssetPath, "Dependencies"), Guid.NewGuid(), null, projectTemplate, projectTemplate.CreateReplacementSet(rootReplacementSet));

            PopulateFolder(folderTemplate, folderNestedProjectsTemplate, rootReplacementSet, "Built In Packages", builtinPackages);
            PopulateFolder(folderTemplate, folderNestedProjectsTemplate, rootReplacementSet, "Imported Packages", importedPacakges);
            PopulateFolder(folderTemplate, folderNestedProjectsTemplate, rootReplacementSet, "External Packages", externalPackages);

            ITemplateToken configPlatform_ConfigurationToken = configPlatformTemplate.Tokens["CONFIGURATION"];
            ITemplateToken configPlatform_PlatformToken = configPlatformTemplate.Tokens["PLATFORM"];
            foreach (CompilationPlatformInfo platform in unityProjectInfo.AvailablePlatforms)
            {
                TemplateReplacementSet replacementSet = configPlatformTemplate.CreateReplacementSet(rootReplacementSet);
                configPlatform_ConfigurationToken.AssignValue(replacementSet, "InEditor");
                configPlatform_PlatformToken.AssignValue(replacementSet, platform.Name);

                replacementSet = configPlatformTemplate.CreateReplacementSet(rootReplacementSet);
                configPlatform_ConfigurationToken.AssignValue(replacementSet, "Player");
                configPlatform_PlatformToken.AssignValue(replacementSet, platform.Name);
            }

            List<string> disabled = new List<string>();

            foreach (CSProjectInfo project in orderedProjects.Select(t => t))
            {
                void ConfigurationTemplateReplace(ITemplatePart templatePart, TemplateReplacementSet replacementSet, string guid, string configuration, string platform)
                {
                    templatePart.Tokens["PROJECT_GUID"].AssignValue(replacementSet, guid.ToString().ToUpper());
                    templatePart.Tokens["PROJECT_CONFIGURATION"].AssignValue(replacementSet, configuration);
                    templatePart.Tokens["PROJECT_PLATFORM"].AssignValue(replacementSet, platform);
                    templatePart.Tokens["SOLUTION_CONFIGURATION"].AssignValue(replacementSet, configuration);
                    templatePart.Tokens["SOLUTION_PLATFORM"].AssignValue(replacementSet, platform);
                }

                void ProcessMappings(Guid guid, string configuration, IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> platforms)
                {
                    foreach (CompilationPlatformInfo platform in unityProjectInfo.AvailablePlatforms)
                    {
                        TemplateReplacementSet replacemetSet = configPlatformMappingTemplate.CreateReplacementSet(rootReplacementSet);
                        ConfigurationTemplateReplace(configPlatformMappingTemplate, replacemetSet, guid.ToString(), configuration, platform.Name);

                        if (platforms.ContainsKey(platform.BuildTarget))
                        {
                            replacemetSet = configPlatformEnabledTemplate.CreateReplacementSet(rootReplacementSet);
                            ConfigurationTemplateReplace(configPlatformEnabledTemplate, replacemetSet, guid.ToString(), configuration, platform.Name);
                        }
                    }
                }

                ProcessMappings(project.Guid, "InEditor", project.InEditorPlatforms);
                ProcessMappings(project.Guid, "Player", project.PlayerPlatforms);
            }

            foreach (CSProjectInfo project in unityProjectInfo.CSProjects.Values)
            {
                ExportProject(unityProjectInfo, project);
            }

            GenerateTopLevelDependenciesProject(unityProjectInfo);

            solutionFileTemplate.Write(solutionFilePath, rootReplacementSet);
        }

        private void GenerateTopLevelDependenciesProject(UnityProjectInfo unityProjectInfo)
        {
            string projectPath = GetProjectFilePath(Utilities.AssetPath, "Dependencies");
            string propsPath = GetProjectFilePath(generatedOutputFolder.FullName, "Dependencies").Replace(".csproj", ".g.props");
            string targetsPath = GetProjectFilePath(generatedOutputFolder.FullName, "Dependencies").Replace(".csproj", ".g.targets");

            ITemplatePart propsFileTemplate = dependenciesPropsTemplate.Root;
            ITemplatePart projectReferenceTemplate = propsFileTemplate.Templates["PROJECT_REFERENCE"];

            TemplateReplacementSet replacementSet = propsFileTemplate.CreateReplacementSet();

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

        private void PopulateFolder(ITemplatePart folderTemplate, ITemplatePart folderNestedProjectsTemplate, TemplateReplacementSet parentReplacementSet, string folderName, List<CSProjectInfo> projects)
        {
            if (projects.Count > 0)
            {
                string folderGuid = Guid.NewGuid().ToString().ToUpper();

                TemplateReplacementSet replacementSet = folderTemplate.CreateReplacementSet(parentReplacementSet);
                folderTemplate.Tokens["FOLDER_NAME"].AssignValue(replacementSet, folderName);
                folderTemplate.Tokens["FOLDER_GUID"].AssignValue(replacementSet, folderGuid);

                foreach (CSProjectInfo project in projects)
                {
                    replacementSet = folderNestedProjectsTemplate.CreateReplacementSet(parentReplacementSet);
                    folderNestedProjectsTemplate.Tokens["FOLDER_GUID"].AssignValue(replacementSet, folderGuid);
                    folderNestedProjectsTemplate.Tokens["CHILD_GUID"].AssignValue(replacementSet, project.Guid.ToString().ToUpper());
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

        private void ProcessProjectEntry(string projectName, string projectPath, Guid projectGuid, IReadOnlyCollection<CSProjectDependency<CSProjectInfo>> projectDependencies, ITemplatePart templatePart, TemplateReplacementSet replacementSet)
        {
            templatePart.Tokens["PROJECT_NAME"].AssignValue(replacementSet, projectName);
            templatePart.Tokens["PROJECT_RELATIVE_PATH"].AssignValue(replacementSet, projectPath);
            templatePart.Tokens["PROJECT_GUID"].AssignValue(replacementSet, projectGuid.ToString().ToUpper());

            ITemplatePart dependencyTemplate = templatePart.Templates["PROJECT_DEPENDENCY"];

            if (projectDependencies != null && projectDependencies.Count > 0)
            {
                foreach (CSProjectDependency<CSProjectInfo> project in projectDependencies)
                {
                    TemplateReplacementSet set = dependencyTemplate.CreateReplacementSet(replacementSet);
                    dependencyTemplate.Tokens["DEPENDENCY_GUID"].AssignValue(set, project.Dependency.Guid.ToString().ToUpper());
                }
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
