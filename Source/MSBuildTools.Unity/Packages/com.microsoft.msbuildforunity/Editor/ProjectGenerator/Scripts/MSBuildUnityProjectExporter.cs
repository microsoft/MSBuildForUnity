// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// This class contains the export logic given info about the current Unity project.
    /// </summary>
    public static class MSBuildUnityProjectExporter
    {
        private const string MSBuildFileSuffix = "msb4u";

        /// <summary>
        /// Exports the core Unity props file.
        /// </summary>
        /// <param name="exporter">The overal exporter to use for creating exporters.</param>
        /// <param name="platform">The compilation platform to export.</param>
        /// <param name="inEditor">Whether to export the inEditor version.</param>
        public static void ExportCoreUnityPropFile(IUnityProjectExporter exporter, CompilationPlatformInfo platform, bool inEditor)
        {
            string configuration = inEditor ? "InEditor" : "Player";
            FileInfo outputFile = new FileInfo(Path.Combine(Utilities.MSBuildProjectFolder, $"{platform.Name}.{configuration}.props"));

            IPlatformPropsExporter propsExporter = (!inEditor && platform.BuildTarget == BuildTarget.WSAPlayer)
                ? CreateWSAPlayerExporter(exporter, outputFile, platform.ScriptingBackend)
                : exporter.CreatePlatformPropsExporter(outputFile, configuration, platform.Name, platform.ScriptingBackend);

            propsExporter.TargetFramework = platform.TargetFramework.AsMSBuildString();

            propsExporter.DefineConstants.AddRange(platform.CommonPlatformDefines);
            propsExporter.DefineConstants.AddRange(inEditor ? platform.AdditionalInEditorDefines : platform.AdditionalPlayerDefines);

            // Common references
            foreach (string reference in platform.CommonPlatformReferences)
            {
                propsExporter.References[Path.GetFileNameWithoutExtension(reference)] = new Uri(reference);
                propsExporter.AssemblySearchPaths.Add(Path.GetDirectoryName(reference));
            }

            // Additional references
            foreach (string reference in (inEditor ? platform.AdditionalInEditorReferences : platform.AdditionalPlayerReferences))
            {
                propsExporter.References[Path.GetFileNameWithoutExtension(reference)] = new Uri(reference);
                propsExporter.AssemblySearchPaths.Add(Path.GetDirectoryName(reference));
            }

            propsExporter.Write();
        }

        /// <summary>
        /// Exports the common MSBuildForUnity.Common.props file.
        /// </summary>
        /// <param name="exporter">The overal exporter to use for creating exporters.</param>
        /// <param name="currentPlayerPlatform">Current unity platform.</param>
        public static void ExportCommonPropsFile(IUnityProjectExporter exporter, Version msb4uVersion, CompilationPlatformInfo currentPlayerPlatform)
        {
            ICommonPropsExporter propsExporter = exporter.CreateCommonPropsExporter(new FileInfo(Path.Combine(Utilities.ProjectPath, "MSBuildForUnity.Common.props")));
            propsExporter.MSBuildForUnityVersion = msb4uVersion;

            string[] versionParts = Application.unityVersion.Split('.');
            propsExporter.UnityMajorVersion = versionParts[0];
            propsExporter.UnityMinorVersion = versionParts[1];
            propsExporter.UnityEditorInstallPath = new DirectoryInfo(Path.GetDirectoryName(EditorApplication.applicationPath));

            propsExporter.GeneratedProjectOutputPath = new DirectoryInfo(Utilities.MSBuildOutputFolder);
            propsExporter.UnityProjectAssetsDirectory = new DirectoryInfo(Utilities.AssetPath);

            propsExporter.CurrentTargetFramework = currentPlayerPlatform.TargetFramework.AsMSBuildString();
            propsExporter.CurrentUnityPlatform = currentPlayerPlatform.Name;

            propsExporter.Write();
        }

        public static void ExportTopLevelDependenciesProject(IUnityProjectExporter exporter, Version msb4uVerison, MSBuildToolsConfig config, DirectoryInfo generatedProjectFolder, UnityProjectInfo unityProjectInfo)
        {
            string projectPath = GetProjectFilePath(Utilities.AssetPath, $"{unityProjectInfo.UnityProjectName}.Dependencies");
            ITopLevelDependenciesProjectExporter projectExporter = exporter.CreateTopLevelDependenciesProjectExporter(new FileInfo(projectPath), generatedProjectFolder);

            projectExporter.MSBuildForUnityVersion = msb4uVerison;
            projectExporter.Guid = config.DependenciesProjectGuid;

            if (unityProjectInfo.AvailablePlatforms != null)
            {
                Dictionary<BuildTarget, CompilationPlatformInfo> allPlatforms = unityProjectInfo.AvailablePlatforms.ToDictionary(t => t.BuildTarget, t => t);
                foreach (CSProjectInfo projectInfo in unityProjectInfo.CSProjects.Values)
                {
                    List<string> platformConditions = GetPlatformConditions(allPlatforms, projectInfo.InEditorPlatforms.Keys);
                    projectExporter.References.Add(new ProjectReference()
                    {
                        ReferencePath = new Uri(GetProjectPath(projectInfo, generatedProjectFolder).FullName),
                        Condition = platformConditions.Count == 0 ? "false" : string.Join(" OR ", platformConditions),
                        IsGenerated = true
                    });
                }
            }

            foreach (string otherProjectFile in unityProjectInfo.ExistingCSProjects)
            {
                projectExporter.References.Add(new ProjectReference()
                {
                    ReferencePath = new Uri(otherProjectFile),
                    IsGenerated = false
                });
            }

            projectExporter.Write();
        }

        private static IPlatformPropsExporter CreateWSAPlayerExporter(IUnityProjectExporter exporter, FileInfo outputFile, ScriptingBackend scriptingBackend)
        {
            IWSAPlayerPlatformPropsExporter uwpExporter = exporter.CreateWSAPlayerPlatformPropsExporter(outputFile, scriptingBackend);

            string minUWPPlatform = EditorUserBuildSettings.wsaMinUWPSDK;
            if (string.IsNullOrWhiteSpace(minUWPPlatform) || new Version(minUWPPlatform) < MSBuildTools.DefaultMinUWPSDK)
            {
                minUWPPlatform = MSBuildTools.DefaultMinUWPSDK.ToString();
            }

            string targetUWPPlatform = EditorUserBuildSettings.wsaUWPSDK;
            if (string.IsNullOrWhiteSpace(targetUWPPlatform))
            {
                targetUWPPlatform = Utilities.GetUWPSDKs().Max().ToString(4);
            }

            uwpExporter.MinimumUWPVersion = minUWPPlatform;
            uwpExporter.TargetUWPVersion = targetUWPPlatform;

            return uwpExporter;
        }

        private static string GetProjectFilePath(DirectoryInfo directory, CSProjectInfo projectInfo)
        {
            return GetProjectFilePath(directory.FullName, projectInfo.Name);
        }

        private static string GetProjectFilePath(string directory, string projectName)
        {
            return Path.Combine(directory, $"{projectName}.{MSBuildFileSuffix}.csproj");
        }

        public static List<string> GetPlatformConditions(IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> platforms, IEnumerable<BuildTarget> dependencyPlatforms)
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

        ///<inherit-doc/>
        public static FileInfo GetProjectPath(CSProjectInfo projectInfo, DirectoryInfo generatedProjectFolder)
        {
            switch (projectInfo.AssemblyDefinitionInfo.AssetLocation)
            {
                case AssetLocation.BuiltInPackage:
                case AssetLocation.External:
                case AssetLocation.PackageLibraryCache:
                    return new FileInfo(GetProjectFilePath(generatedProjectFolder, projectInfo));
                case AssetLocation.Project:
                case AssetLocation.Package:
                    return new FileInfo(GetProjectFilePath(projectInfo.AssemblyDefinitionInfo.Directory, projectInfo));
                default:
                    throw new InvalidOperationException("The project's assembly definition file is in an unknown location.");
            }
        }

    }
}
#endif