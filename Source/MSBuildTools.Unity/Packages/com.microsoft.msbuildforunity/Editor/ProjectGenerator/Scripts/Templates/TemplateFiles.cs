// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Templates
{
    /// <summary>
    /// A helper class to manage (and locate) all the templates.
    /// </summary>
    public class TemplateFiles
    {
        private const string TemplateFilesFolderName = "MSBuildTemplates";
        private const string MSBuildSolutionTemplateName = "SolutionTemplate.sln.template";
        private const string MSBuildForUnityCommonPropsTemplateName = "MSBuildForUnity.Common.props.template";
        private const string SDKProjectFileTemplateName = "SDKProjectTemplate.csproj.template";
        private const string SDKGeneratedProjectFileTemplateName = "SDKProjectTemplate.g.csproj.template";
        private const string SDKProjectPropsFileTemplateName = "SDKProjectTemplate.g.props.template";
        private const string SDKProjectTargetsFileTemplateName = "SDKProjectTemplate.g.targets.template";
        private const string DependenciesProjectFileTemplateName = "DependenciesProjectTemplate.csproj.template";
        private const string DependenciesPropsFileTemplateName = "DependenciesProjectTemplate.g.props.template";
        private const string DependenciesTargetsFileTemplateName = "DependenciesProjectTemplate.g.targets.template";
        private const string PlatformPropsTemplateName = "Platform.Configuration.Any.props.template";
        private const string EditorPropsTemplateName = "Editor.InEditor.Any.props.template";
        private const string SpecifcPlatformPropsTemplateRegex = @"[a-zA-Z]+\.[a-zA-Z]+\.[a-zA-Z0-9]*\.props.template";
        private const string PluginMetaFileTemplateRegex = @"Plugin\.([a-zA-Z]*)\.meta.template";
        private const string BuildProjectsTemplateName = "BuildProjects.proj.template";
        private const string NuGetConfigFileName = "NuGet.config.template";

        private static TemplateFiles instance;

        /// <summary>
        /// Gets the singleton instance (created on demand) of this class.
        /// </summary>
        public static TemplateFiles Instance => instance ?? (instance = new TemplateFiles());

        /// <summary>
        /// Gets the MSBuild Solution file (.sln) template path.
        /// </summary>
        public FileInfo MSBuildSolutionTemplatePath { get; }

        /// <summary>
        /// The path to the Directory.Build.props file template.
        /// </summary>
        public FileInfo MSBuildForUnityCommonPropsTemplatePath { get; }

        /// <summary>
        /// The path to the dependencies project file template.
        /// </summary>
        public FileInfo DependenciesProjectTemplatePath { get; }

        /// <summary>
        /// The path to the dependencies project props template.
        /// </summary>
        public FileInfo DependenciesPropsTemplatePath { get; }

        /// <summary>
        /// The path to the dependencies project targets template.
        /// </summary>
        public FileInfo DependenciesTargetsTemplatePath { get; }

        /// <summary>
        /// Gets the MSBuild C# SDK Project file (.csproj) template path.
        /// </summary>
        public FileInfo SDKProjectFileTemplatePath { get; }

        /// <summary>
        /// Gets the MSBuild C# SDK Project file (.csproj) template path for generated projects (Read-Only).
        /// </summary>
        public FileInfo SDKGeneratedProjectFileTemplatePath { get; }

        /// <summary>
        /// Gets the MSBuild C# SDK Project file (.csproj) template path.
        /// </summary>
        public FileInfo SDKProjectPropsFileTemplatePath { get; }

        /// <summary>
        /// Gets the MSBuild C# SDK Project file (.csproj) template path.
        /// </summary>
        public FileInfo SDKProjectTargetsFileTemplatePath { get; }

        /// <summary>
        /// Gets the MSBuild Platform Props file (.props) template path.
        /// </summary>
        public FileInfo PlatformPropsTemplatePath { get; }

        /// <summary>
        /// Gets the BuildProjects.proj MSBuild file template path.
        /// </summary>
        public string BuildProjectsTemplatePath { get; }

        /// <summary>
        /// Gets the NuGet.config MSBuild file path.
        /// </summary>
        public string NuGetConfigPath { get; }

        /// <summary>
        /// Gets a list of specialized platform templates.
        /// </summary>
        public IReadOnlyDictionary<string, FileInfo> PlatformTemplates { get; }

        /// <summary>
        /// Gets a list of meta files for plugins templates.
        /// </summary>
        public IReadOnlyDictionary<BuildTargetGroup, FileInfo> PluginMetaTemplatePaths { get; }

        /// <summary>
        /// Gets a list of all other files included among the templates.
        /// </summary>
        public IReadOnlyList<string> OtherFiles { get; }

        private TemplateFiles()
        {
            AssetDatabase.Refresh();

            string[] templateFolders = AssetDatabase.FindAssets(TemplateFilesFolderName);
            Utilities.GetPathsFromGuidsInPlace(templateFolders);

            if (templateFolders.Length == 0)
            {
                Debug.LogError($"Templates folder '{TemplateFilesFolderName}' not found.");
            }
            else if (templateFolders.Length > 1)
            {
                Debug.LogWarning($"Strange, more than one directory exists for template files:\n {string.Join("\n", templateFolders)}");
            }

            string[] files = AssetDatabase.FindAssets("*", templateFolders);
            Utilities.GetPathsFromGuidsInPlace(files, fullPaths: true);

            Dictionary<string, string> fileNamesMaps = files.ToDictionary(t => Path.GetFileName(t));

            MSBuildSolutionTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "MSBuild Solution", MSBuildSolutionTemplateName));
            MSBuildForUnityCommonPropsTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "Directory.Build Props File", MSBuildForUnityCommonPropsTemplateName));
            DependenciesProjectTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "Dependencies CSProject File", DependenciesProjectFileTemplateName));
            DependenciesPropsTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "Dependencies Props File", DependenciesPropsFileTemplateName));
            DependenciesTargetsTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "Dependencies Props File", DependenciesTargetsFileTemplateName));
            SDKProjectFileTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "SDK Project", SDKProjectFileTemplateName));
            SDKGeneratedProjectFileTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "Generated SDK Project", SDKGeneratedProjectFileTemplateName));
            SDKProjectPropsFileTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "SDK Project Props", SDKProjectPropsFileTemplateName));
            SDKProjectTargetsFileTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "SDK Project Targets", SDKProjectTargetsFileTemplateName));
            PlatformPropsTemplatePath = new FileInfo(GetExpectedTemplatesPath(fileNamesMaps, "Platform Props", PlatformPropsTemplateName));
            BuildProjectsTemplatePath = GetExpectedTemplatesPath(fileNamesMaps, "MSBuild Build Projects Proj", BuildProjectsTemplateName);
            NuGetConfigPath = GetExpectedTemplatesPath(fileNamesMaps, "MSBuild NuGet.config", NuGetConfigFileName);

            // Get specific platforms
            Dictionary<string, FileInfo> platformTemplates = new Dictionary<string, FileInfo>();
            Dictionary<BuildTargetGroup, FileInfo> metaFileTemplates = new Dictionary<BuildTargetGroup, FileInfo>();

            HashSet<string> toRemove = new HashSet<string>();
            foreach (KeyValuePair<string, string> pair in fileNamesMaps)
            {
                if (Regex.IsMatch(pair.Key, SpecifcPlatformPropsTemplateRegex))
                {
                    platformTemplates.Add(pair.Key, new FileInfo(pair.Value));
                    toRemove.Add(pair.Key);
                }
                else
                {
                    Match match = Regex.Match(pair.Key, PluginMetaFileTemplateRegex);
                    if (match.Success)
                    {
                        string value = match.Groups[1].Captures[0].Value;
                        FileInfo fileInfo = new FileInfo(pair.Value);
                        if (Equals(value, "Editor"))
                        {
                            metaFileTemplates.Add(BuildTargetGroup.Unknown, fileInfo);
                        }
                        else if (Enum.TryParse(value, out BuildTargetGroup buildTargetGroup))
                        {
                            metaFileTemplates.Add(buildTargetGroup, fileInfo);
                        }
                        else
                        {
                            Debug.LogError($"Matched meta template but failed to parse it: {pair.Key}");
                        }

                        toRemove.Add(pair.Key);
                    }
                }
            }

            foreach (string item in toRemove)
            {
                fileNamesMaps.Remove(item);
            }

            PlatformTemplates = new ReadOnlyDictionary<string, FileInfo>(platformTemplates);
            PluginMetaTemplatePaths = new ReadOnlyDictionary<BuildTargetGroup, FileInfo>(metaFileTemplates);

            OtherFiles = new ReadOnlyCollection<string>(fileNamesMaps.Values.ToList());
        }

        /// <summary>
        /// Gets the correct platform template file path.
        /// </summary>
        /// <param name="platform">The platform of the requested template.</param>
        /// <param name="configuration">The configuration of the requested template.</param>
        /// <returns>The absolute file path for the platform template to use.</returns>
        public FileInfo GetTemplateFilePathForPlatform(string platform, string configuration, ScriptingBackend scriptingBackend)
        {
            if (PlatformTemplates.TryGetValue($"{platform}.{configuration}.{scriptingBackend.ToString()}.props.template", out FileInfo templatePath)
                || PlatformTemplates.TryGetValue($"{platform}.{configuration}.Any.props.template", out templatePath)
                || PlatformTemplates.TryGetValue($"{platform}.Configuration.Any.props.template", out templatePath))
            {
                return templatePath;
            }
            else
            {
                return PlatformPropsTemplatePath;
            }
        }

        private string GetExpectedTemplatesPath(Dictionary<string, string> fileNamesMaps, string displayName, string fileName)
        {
            if (fileNamesMaps.TryGetValue(fileName, out string path))
            {
                fileNamesMaps.Remove(fileName);
                return path;
            }
            else
            {
                Debug.LogError($"Could not find {displayName} template with filename '{fileName}'");
                return string.Empty;
            }
        }
    }
}
#endif