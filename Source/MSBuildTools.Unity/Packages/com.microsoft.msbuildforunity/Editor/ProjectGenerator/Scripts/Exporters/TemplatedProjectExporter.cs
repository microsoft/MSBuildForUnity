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
        private readonly DirectoryInfo propsOutputFolder;

        private readonly FileTemplate projectFileTemplate;
        private readonly FileTemplate propsFileTemplate;
        private readonly FileTemplate targetsFileTemplate;

        private readonly FileTemplate solutionFileTemplate;

        /// <summary>
        /// Creates a new instance of the template driven <see cref="IProjectExporter"/>.
        /// </summary>
        /// <param name="propsOutputFolder">The output folder for the projects and props.</param>
        /// <param name="solutionFileTemplatePath">The path to the solution template.</param>
        /// <param name="projectFileTemplatePath">The path to the C# project file template.</param>
        /// <param name="projectPropsFileTemplatePath">The path to the props file template.</param>
        /// <param name="projectTargetsFileTemplatePath">The path to the targets file template.</param>
        public TemplatedProjectExporter(DirectoryInfo propsOutputFolder, FileInfo solutionFileTemplatePath, FileInfo projectFileTemplatePath, FileInfo projectPropsFileTemplatePath, FileInfo projectTargetsFileTemplatePath)
        {
            this.propsOutputFolder = propsOutputFolder;

            FileTemplate.TryParseTemplate(projectFileTemplatePath, out projectFileTemplate);
            FileTemplate.TryParseTemplate(projectPropsFileTemplatePath, out propsFileTemplate);
            FileTemplate.TryParseTemplate(projectTargetsFileTemplatePath, out targetsFileTemplate);

            FileTemplate.TryParseTemplate(solutionFileTemplatePath, out solutionFileTemplate);
        }

        /// <inheritdoc />
        public FileInfo GetProjectPath(CSProjectInfo projectInfo)
        {
            return new FileInfo(Path.Combine(propsOutputFolder.FullName, $"{projectInfo.Name}.csproj"));
        }

        ///<inherit-doc/>
        public void ExportProject(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            string projectPath = GetProjectPath(projectInfo).FullName;

            if (File.Exists(projectPath))
            {
                File.Delete(projectPath);
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

            if (File.Exists(projectPath))
            {
                Debug.Log($"Skipping replacing the existing C# project file {projectInfo.Name}");
            }
            else
            {
                projectFileTemplate.Write(projectPath, projectFileTemplate.Root.CreateReplacementSet());
            }
        }

        private bool TryExportPropsFile(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            ITemplatePart rootTemplatePart = propsFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();

            ITemplatePart projectReferenceSetTemplatePart = rootTemplatePart.Templates["PROJECT_REFERENCE_SET"];
            ITemplatePart sourceIncludeTemplatePart = rootTemplatePart.Templates["SOURCE_INCLUDE"];

            string projectPath = GetProjectPath(projectInfo).FullName;

            //Dictionary<Guid, string> sourceGuidToClassName = new Dictionary<Guid, string>();
            foreach (SourceFileInfo source in projectInfo.AssemblyDefinitionInfo.GetSources())
            {
                //sourceGuidToClassName.Add(source.Guid, source.ClassType?.FullName);
                ProcessSourceFile(projectInfo, source, sourceIncludeTemplatePart, rootReplacementSet);
            }

            // For now don't create .csmaps; not used yet anyways
            //File.WriteAllLines(Path.Combine(propsOutputFolder.FullName, $"{projectInfo.Guid.ToString()}.csmap"), sourceGuidToClassName.Select(t => $"{t.Key.ToString("N")}:{t.Value}"));

            HashSet<string> inEditorSearchPaths = new HashSet<string>(), playerSearchPaths = new HashSet<string>();
            CreateProjectReferencesSet(projectInfo, projectReferenceSetTemplatePart, rootReplacementSet, inEditorSearchPaths, true);
            CreateProjectReferencesSet(projectInfo, projectReferenceSetTemplatePart, rootReplacementSet, playerSearchPaths, false);

            rootTemplatePart.Tokens["PROJECT_GUID"].AssignValue(rootReplacementSet, projectInfo.Guid.ToString());
            rootTemplatePart.Tokens["ALLOW_UNSAFE"].AssignValue(rootReplacementSet, projectInfo.AssemblyDefinitionInfo.allowUnsafeCode.ToString());
            rootTemplatePart.Tokens["LANGUAGE_VERSION"].AssignValue(rootReplacementSet, MSBuildTools.CSharpVersion);
            rootTemplatePart.Tokens["DEVELOPMENT_BUILD"].AssignValue(rootReplacementSet, "false");
            rootTemplatePart.Tokens["IS_EDITOR_ONLY_TARGET"].AssignValue(rootReplacementSet, (projectInfo.ProjectType == ProjectType.EditorAsmDef || projectInfo.ProjectType == ProjectType.PredefinedEditorAssembly).ToString());
            rootTemplatePart.Tokens["UNITY_EDITOR_INSTALL_FOLDER"].AssignValue(rootReplacementSet, Path.GetDirectoryName(EditorApplication.applicationPath) + "\\");
            rootTemplatePart.Tokens["DEFAULT_PLATFORM"].AssignValue(rootReplacementSet, unityProjectInfo.AvailablePlatforms.First(t => t.BuildTarget == BuildTarget.StandaloneWindows).Name);
            rootTemplatePart.Tokens["SUPPORTED_PLATFORMS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", unityProjectInfo.AvailablePlatforms.Select(t => t.Name)));
            rootTemplatePart.Tokens["INEDITOR_ASSEMBLY_SEARCH_PATHS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", inEditorSearchPaths));
            rootTemplatePart.Tokens["PLAYER_ASSEMBLY_SEARCH_PATHS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", playerSearchPaths));
            rootTemplatePart.Tokens["PLATFORM_PROPS_FOLDER_PATH"].AssignValue(rootReplacementSet, propsOutputFolder.FullName);

            propsFileTemplate.Write(projectPath.Replace("csproj", "g.props"), rootReplacementSet);
            return true;
        }

        private bool TryExportTargetsFile(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            string projectPath = GetProjectPath(projectInfo).FullName;
            ITemplatePart rootTemplatePart = targetsFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();
            ITemplatePart supportedPlatformBuildTemplate = rootTemplatePart.Templates["SUPPORTED_PLATFORM_BUILD_CONDITION"];

            PopulateSupportedPlatformBuildConditions(supportedPlatformBuildTemplate, rootReplacementSet, "InEditor", projectInfo.InEditorPlatforms);
            PopulateSupportedPlatformBuildConditions(supportedPlatformBuildTemplate, rootReplacementSet, "Player", projectInfo.PlayerPlatforms);

            targetsFileTemplate.Write(projectPath.Replace("csproj", "g.targets"), rootReplacementSet);

            return true;
        }

        ///<inherit-doc/>
        public void ExportSolution(UnityProjectInfo unityProjectInfo)
        {
            string solutionFilePath = Path.Combine(propsOutputFolder.FullName, $"{unityProjectInfo.UnityProjectName}.sln");

            if (File.Exists(solutionFilePath))
            {
                File.Delete(solutionFilePath);
            }
            ITemplatePart rootTemplatePart = solutionFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();

            ITemplatePart projectTemplate = rootTemplatePart.Templates["PROJECT"];
            ITemplatePart configPlatformTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM"];
            ITemplatePart configPlatformMappingTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM_MAPPING"];
            ITemplatePart configPlatformEnabledTemplate = rootTemplatePart.Templates["CONFIGURATION_PLATFORM_ENABLED"];

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
            foreach (CSProjectInfo project in orderedProjects)
            {
                TemplateReplacementSet replacementSet = projectTemplate.CreateReplacementSet(rootReplacementSet);
                ProcessProjectEntry(project, projectTemplate, replacementSet);
            }

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

            solutionFileTemplate.Write(solutionFilePath, rootReplacementSet);
        }

        private void ProcessSourceFile(CSProjectInfo projectInfo, SourceFileInfo sourceFile, ITemplatePart templatePart, TemplateReplacementSet parentReplacementSet)
        {
            string linkPath = Utilities.GetRelativePath(projectInfo.AssemblyDefinitionInfo.Directory.FullName, sourceFile.File.FullName);

            string relativeSourcePath;

            switch (sourceFile.AssetLocation)
            {
                case AssetLocation.BuiltInPackage:
                    relativeSourcePath = sourceFile.File.FullName;
                    return;
                case AssetLocation.Project:
                    relativeSourcePath = $"..\\..\\{Utilities.GetAssetsRelativePathFrom(sourceFile.File.FullName)}";
                    break;
                case AssetLocation.Package:
                    relativeSourcePath = sourceFile.File.FullName.Replace(Utilities.ProjectPath, "..\\..\\");
                    break;
                case AssetLocation.PackageLibraryCache:
                    relativeSourcePath = sourceFile.File.FullName.Replace(Utilities.ProjectPath, "..\\..\\");
                    break;
                case AssetLocation.External:
                    relativeSourcePath = sourceFile.File.FullName;
                    break;
                default: throw new InvalidDataException("Unknown asset location.");
            }

            TemplateReplacementSet replacementSet = templatePart.CreateReplacementSet(parentReplacementSet);
            templatePart.Tokens["RELATIVE_SOURCE_PATH"].AssignValue(replacementSet, relativeSourcePath);
            templatePart.Tokens["PROJECT_LINK_PATH"].AssignValue(replacementSet, linkPath);
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

                TemplateReplacementSet replacementSet = projectReferenceTemplatePart.CreateReplacementSet(templateReplacementSet);
                projectReferenceTemplatePart.Tokens["REFERENCE"].AssignValue(replacementSet, $"{dependency.Dependency.Name}.csproj");
                //projectReferenceTemplatePart.Tokens["HINT_PATH"].AssignValue(replacementSet, GetProjectPath(dependency.Dependency).AbsolutePath);
                projectReferenceTemplatePart.Tokens["CONDITION"].AssignValue(replacementSet, platformConditions.Count == 0 ? "false" : string.Join(" OR ", platformConditions));
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
                pluginReferenceTemplatePart.Tokens["HINT_PATH"].AssignValue(replacementSet, dependency.Dependency.ReferencePath.AbsolutePath);
                pluginReferenceTemplatePart.Tokens["CONDITION"].AssignValue(replacementSet, platformConditions.Count == 0 ? "false" : string.Join(" OR ", platformConditions));

                additionalSearchPaths.Add(Path.GetDirectoryName(dependency.Dependency.ReferencePath.AbsolutePath));
            }

            templatePart.Tokens["REFERENCE_CONFIGURATION"].AssignValue(templateReplacementSet, inEditor ? "InEditor" : "Player");
        }

        private List<string> GetPlatformConditions(IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> platforms, HashSet<BuildTarget> dependencyPlatforms)
        {
            List<string> toReturn = new List<string>();

            foreach (KeyValuePair<BuildTarget, CompilationPlatformInfo> pair in platforms)
            {
                if (dependencyPlatforms.Contains(pair.Key))
                {
                    string platformName = pair.Value.Name;
                    toReturn.Add($"'$(UnityPlatform)' == '{platformName}'");
                }
            }

            return toReturn;
        }

        private void ProcessProjectEntry(CSProjectInfo projectInfo, ITemplatePart templatePart, TemplateReplacementSet replacementSet)
        {
            string projectPath = GetProjectPath(projectInfo).FullName;

            templatePart.Tokens["PROJECT_NAME"].AssignValue(replacementSet, projectInfo.Name);
            templatePart.Tokens["PROJECT_RELATIVE_PATH"].AssignValue(replacementSet, Path.GetFileName(projectPath));
            templatePart.Tokens["PROJECT_GUID"].AssignValue(replacementSet, projectInfo.Guid.ToString().ToUpper());

            ITemplatePart dependencyTemplate = templatePart.Templates["PROJECT_DEPENDENCY"];

            if (projectInfo.ProjectDependencies.Count > 0)
            {
                foreach (CSProjectDependency<CSProjectInfo> project in projectInfo.ProjectDependencies)
                {
                    TemplateReplacementSet set = dependencyTemplate.CreateReplacementSet(replacementSet);
                    dependencyTemplate.Tokens["DEPENDENCY_GUID"].AssignValue(set, project.Dependency.Guid.ToString().ToUpper());
                }
            }
        }
    }
}
#endif
