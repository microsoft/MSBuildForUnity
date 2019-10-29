// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private readonly string solutionFileTemplateText;

        public TemplatedProjectExporter(DirectoryInfo propsOutputFolder, FileInfo solutionFileTemplatePath, FileInfo projectFileTemplatePath, FileInfo projectPropsFileTemplatePath, FileInfo projectTargetsFileTemplatePath)
        {
            this.propsOutputFolder = propsOutputFolder;

            FileTemplate.TryParseTemplate(projectFileTemplatePath, out projectFileTemplate);
            FileTemplate.TryParseTemplate(projectPropsFileTemplatePath, out propsFileTemplate);
            FileTemplate.TryParseTemplate(projectTargetsFileTemplatePath, out targetsFileTemplate);

            solutionFileTemplateText = File.ReadAllText(solutionFileTemplatePath.FullName);
        }

        public Uri GetProjectPath(CSProjectInfo projectInfo)
        {
            return new Uri(Path.Combine(propsOutputFolder.FullName, $"{projectInfo.Name}.csproj"));
        }

        public void ExportProject(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo)
        {
            string projectPath = GetProjectPath(projectInfo).AbsolutePath;

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

            string projectPath = GetProjectPath(projectInfo).AbsolutePath;

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
            string projectPath = GetProjectPath(projectInfo).AbsolutePath;
            ITemplatePart rootTemplatePart = targetsFileTemplate.Root;
            TemplateReplacementSet rootReplacementSet = rootTemplatePart.CreateReplacementSet();
            ITemplatePart supportedPlatformBuildTemplate = rootTemplatePart.Templates["SUPPORTED_PLATFORM_BUILD_CONDITION"];

            PopulateSupportedPlatformBuildConditions(supportedPlatformBuildTemplate, rootReplacementSet, "InEditor", projectInfo.InEditorPlatforms);
            PopulateSupportedPlatformBuildConditions(supportedPlatformBuildTemplate, rootReplacementSet, "Player", projectInfo.PlayerPlatforms);

            targetsFileTemplate.Write(projectPath.Replace("csproj", "g.targets"), rootReplacementSet);

            return true;
        }

        public void ExportSolution(UnityProjectInfo unityProjectInfo)
        {
            string solutionFilePath = Path.Combine(propsOutputFolder.FullName, $"{unityProjectInfo.UnityProjectName}.sln");

            if (File.Exists(solutionFilePath))
            {
                File.Delete(solutionFilePath);
            }
            string solutionTemplateText = solutionFileTemplateText;

            if (Utilities.TryGetTextTemplate(solutionFileTemplateText, "PROJECT", out string projectEntryTemplate, out string projectEntryTemplateBody)
                && Utilities.TryGetTextTemplate(solutionFileTemplateText, "CONFIGURATION_PLATFORM", out string configurationPlatformEntry, out string configurationPlatformEntryBody)
                && Utilities.TryGetTextTemplate(solutionFileTemplateText, "CONFIGURATION_PLATFORM_MAPPING", out string configurationPlatformMappingTemplate, out string configurationPlatformMappingTemplateBody)
                && Utilities.TryGetTextTemplate(solutionFileTemplateText, "CONFIGURATION_PLATFORM_ENABLED", out string configurationPlatformEnabledTemplate, out string configurationPlatformEnabledTemplateBody))
            {
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

                IEnumerable<string> projectEntries = orderedProjects.Select(t => GetProjectEntry(t, projectEntryTemplateBody));

                string[] twoConfigs = new string[] {
                    configurationPlatformEntryBody.Replace("<Configuration>", "InEditor"),
                    configurationPlatformEntryBody.Replace("<Configuration>", "Player")
                };

                IEnumerable<string> configPlatforms = twoConfigs
                    .SelectMany(t => unityProjectInfo.AvailablePlatforms.Select(p => t.Replace("<Platform>", p.Name.ToString())));

                List<string> configurationMappings = new List<string>();
                List<string> disabled = new List<string>();

                foreach (CSProjectInfo project in orderedProjects.Select(t => t))
                {
                    string ConfigurationTemplateReplace(string template, string guid, string configuration, string platform)
                    {
                        return Utilities.ReplaceTokens(template, new Dictionary<string, string>()
                        {
                            { "<PROJECT_GUID_TOKEN>", guid.ToString().ToUpper() },
                            { "<PROJECT_CONFIGURATION_TOKEN>", configuration },
                            { "<PROJECT_PLATFORM_TOKEN>", platform },
                            { "<SOLUTION_CONFIGURATION_TOKEN>", configuration },
                            { "<SOLUTION_PLATFORM_TOKEN>", platform },
                        });
                    }

                    void ProcessMappings(Guid guid, string configuration, IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> platforms)
                    {
                        foreach (CompilationPlatformInfo platform in unityProjectInfo.AvailablePlatforms)
                        {
                            configurationMappings.Add(ConfigurationTemplateReplace(configurationPlatformMappingTemplateBody, guid.ToString(), configuration, platform.Name));

                            if (platforms.ContainsKey(platform.BuildTarget))
                            {
                                configurationMappings.Add(ConfigurationTemplateReplace(configurationPlatformEnabledTemplateBody, guid.ToString(), configuration, platform.Name));
                            }
                        }
                    }

                    ProcessMappings(project.Guid, "InEditor", project.InEditorPlatforms);
                    ProcessMappings(project.Guid, "Player", project.PlayerPlatforms);
                }

                solutionTemplateText = Utilities.ReplaceTokens(solutionFileTemplateText, new Dictionary<string, string>()
                {
                    { projectEntryTemplate, string.Join(Environment.NewLine, projectEntries)},
                    { configurationPlatformEntry, string.Join(Environment.NewLine, configPlatforms)},
                    { configurationPlatformMappingTemplate, string.Join(Environment.NewLine, configurationMappings) },
                    { configurationPlatformEnabledTemplate, string.Join(Environment.NewLine, disabled) }
                });
            }
            else
            {
                Debug.LogError("Failed to find Project and/or Configuration_Platform templates in the solution template file.");
            }

            foreach (CSProjectInfo project in unityProjectInfo.CSProjects.Values)
            {
                ExportProject(unityProjectInfo, project);
            }

            File.WriteAllText(solutionFilePath, solutionTemplateText);
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

        private string GetProjectEntry(CSProjectInfo projectInfo, string projectEntryTemplateBody)
        {
            string projectPath = GetProjectPath(projectInfo).AbsolutePath;

            StringBuilder toReturn = new StringBuilder();

            toReturn.AppendLine(Utilities.ReplaceTokens(projectEntryTemplateBody, new Dictionary<string, string>() {
                        { "<PROJECT_NAME>", projectInfo.Name },
                        { "<PROJECT_RELATIVE_PATH>", Path.GetFileName(projectPath) },
                        { "<PROJECT_GUID>", projectInfo.Guid.ToString().ToUpper() } }));

            if (projectInfo.ProjectDependencies.Count > 0)
            {
                string projectDependencyStartSection = "    ProjectSection(ProjectDependencies) = postProject";
                string projectDependencyGuid = "        {<DependencyGuid>} = {<DependencyGuid>}";
                string projectDependencyStopSection = "    EndProjectSection";
                toReturn.AppendLine(projectDependencyStartSection);

                foreach (CSProjectDependency<CSProjectInfo> project in projectInfo.ProjectDependencies)
                {
                    toReturn.AppendLine(projectDependencyGuid.Replace("<DependencyGuid>", project.Dependency.Guid.ToString().ToUpper()));
                }

                toReturn.AppendLine(projectDependencyStopSection);
            }
            toReturn.Append("EndProject");
            return toReturn.ToString();
        }
    }
}
#endif
