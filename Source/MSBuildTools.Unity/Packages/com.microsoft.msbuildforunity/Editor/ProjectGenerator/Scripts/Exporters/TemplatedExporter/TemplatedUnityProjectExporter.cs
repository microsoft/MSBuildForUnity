// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// This interface exposes teh APIs for exporting projects.
    /// </summary>
    public class TemplatedUnityProjectExporter : IUnityProjectExporter
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
        /// Creates a new instance of the template driven <see cref="IUnityProjectExporter"/>.
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
        public TemplatedUnityProjectExporter(DirectoryInfo generatedOutputFolder, FileInfo solutionFileTemplatePath, FileInfo projectFileTemplatePath, FileInfo generatedProjectFileTemplatePath, FileInfo projectPropsFileTemplatePath, FileInfo projectTargetsFileTemplatePath, FileInfo msbuildForUnityCommonTemplatePath, FileInfo dependenciesProjectTemplatePath, FileInfo dependenciesPropsTemplatePath, FileInfo dependenciesTargetsTemplatePath)
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
        
        public string GetSolutionFilePath(UnityProjectInfo unityProjectInfo)
        {
            return Path.Combine(Utilities.AssetPath, $"{unityProjectInfo.UnityProjectName}.{MSBuildFileSuffix}.sln");
        }

        public ICommonPropsExporter CreateCommonPropsExporter(FileInfo path)
        {
            return new TemplatedCommonPropsExporter(msbuildForUnityCommonTemplate, path);
        }
        
        public ITopLevelDependenciesProjectExporter CreateTopLevelDependenciesProjectExporter(FileInfo projectPath, DirectoryInfo generatedProjectFolder)
        {
            FileInfo propsFilePath = new FileInfo(Path.Combine(generatedProjectFolder.FullName, projectPath.Name.Replace(".csproj", ".g.props")));
            FileInfo targetsFilePath = new FileInfo(Path.Combine(generatedProjectFolder.FullName, projectPath.Name.Replace(".csproj", ".g.targets")));

            return new TemplatedTopLevelDependenciesProjectExporter(dependenciesProjectTemplate, dependenciesPropsTemplate, dependenciesTargetsTemplate, projectPath, propsFilePath, targetsFilePath);
        }

        public ICSharpProjectExporter CreateCSharpProjectExporter(FileInfo projectPath, DirectoryInfo generatedProjectFolder, bool isGenerated)
        {
            FileInfo propsFilePath = new FileInfo(Path.Combine(generatedProjectFolder.FullName, projectPath.Name.Replace(".csproj", ".g.props")));
            FileInfo targetsFilePath = new FileInfo(Path.Combine(generatedProjectFolder.FullName, projectPath.Name.Replace(".csproj", ".g.targets")));

            return new TemplatedCSharpProjectExporter(isGenerated ? generatedProjectFileTemplate : projectFileTemplate, propsFileTemplate, targetsFileTemplate, projectPath, propsFilePath, targetsFilePath);
        }

        public ISolutionExporter CreateSolutionExporter(ILogger logger, FileInfo outputPath)
        {
            return new TemplatedSolutionExporter(logger, solutionFileTemplate, outputPath);
        }

        public IPlatformPropsExporter CreatePlatformPropsExporter(FileInfo path, string unityConfiguration, string unityPlatform, ScriptingBackend scriptingBackend)
        {
            if (!FileTemplate.TryParseTemplate(TemplateFiles.Instance.GetTemplateFilePathForPlatform(unityPlatform, unityConfiguration, scriptingBackend), out FileTemplate fileTemplate))
            {
                throw new InvalidOperationException("Failed to parse template file for common props.");
            }

            return new TemplatedPlatformPropsExporter(fileTemplate, path);
        }

        public IWSAPlayerPlatformPropsExporter CreateWSAPlayerPlatformPropsExporter(FileInfo path, ScriptingBackend scriptingBackend)
        {
            if (!FileTemplate.TryParseTemplate(TemplateFiles.Instance.GetTemplateFilePathForPlatform("WSA", "Player", scriptingBackend), out FileTemplate fileTemplate))
            {
                throw new InvalidOperationException("Failed to parse template file for common props.");
            }

            return new TemplatedWSAPlayerPlatformPropsExporter(fileTemplate, path);
        }
    }
}
#endif
