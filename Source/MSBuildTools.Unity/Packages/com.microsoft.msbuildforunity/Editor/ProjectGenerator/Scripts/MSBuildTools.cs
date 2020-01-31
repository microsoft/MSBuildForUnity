// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
using Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter;
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using Debug = UnityEngine.Debug;

namespace Microsoft.Build.Unity.ProjectGeneration
{

    /// <summary>
    /// Class that exposes the MSBuild project generation operation.
    /// </summary>
    [InitializeOnLoad]
    public static class MSBuildTools
    {
        private static bool CSProjectAssetChanged = false;
        private static bool AssemblyDefinitionAssetChanged = false;
        private static bool CSProjectAssetRemoved = false;

        private class AssetPostprocessor : UnityEditor.AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                // Check for deleted path first
                CheckPathsForChanges(deletedAssets);
                CSProjectAssetRemoved = CSProjectAssetChanged;

                CheckPathsForChanges(importedAssets, movedAssets, movedFromAssetPaths);

                if (AssemblyDefinitionAssetChanged || CSProjectAssetChanged)
                {
                    RefreshGeneratedOutput(forceGenerateEverything: false, forceCompleteGeneration: false);
                }
            }

            public static void CheckPathsForChanges(params string[][] assetPathSets)
            {
                foreach (string[] assetPathSet in assetPathSets)
                {
                    foreach (string assetPath in assetPathSet)
                    {
                        if (CSProjectAssetChanged && AssemblyDefinitionAssetChanged)
                        {
                            // Break out early since both are now true
                            return;
                        }

                        string extension = Path.GetExtension(assetPath).ToLower();

                        if (extension == ".csproj")
                        {
                            // Ignore generated projects
                            if (!assetPath.EndsWith(".msb4u.csproj", StringComparison.CurrentCultureIgnoreCase))
                            {
                                CSProjectAssetChanged = true;
                            }
                        }
                        else if (extension == ".asmdef")
                        {
                            AssemblyDefinitionAssetChanged = true;
                        }
                    }
                }
            }
        }

        private class BuildTargetChanged : IActiveBuildTargetChanged
        {
            public int callbackOrder => 0;

            public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
            {
                if (EditorAnalyticsSessionInfo.elapsedTime > 0)
                {
                    RefreshGeneratedOutput(forceGenerateEverything: true, forceCompleteGeneration: false);
                }
            }
        }

        public static readonly Dictionary<BuildTarget, string> SupportedBuildTargets = new Dictionary<BuildTarget, string>()
        {
            { BuildTarget.StandaloneWindows, "Win" },
            { BuildTarget.StandaloneWindows64, "Win64" },
            { BuildTarget.StandaloneOSX, "OSXUniversal" },
            { BuildTarget.StandaloneLinux64, "Linux64" },
#if UNITY_2018
            { BuildTarget.StandaloneLinux, "Linux" },
            { BuildTarget.StandaloneLinuxUniversal, "LinuxUniversal" },
#endif
            { BuildTarget.iOS, "iOS" },
            { BuildTarget.Android, "Android" },
            { BuildTarget.WSAPlayer, "WindowsStoreApps" }
        };

        public const string CSharpVersion = "7.3";
        public const string FullGeneration = "MSBuild/Full Generation Enabled";

        // On master branches this version must be incremented by 1 as to what is specified in the package.json.
        // This is because the build pipelines do that and append a prerelease.
        public static readonly Version MSBuildForUnityVersion = new Version(0, 9, 1);
        public static readonly Version DefaultMinUWPSDK = new Version("10.0.14393.0");

        private static UnityProjectInfo unityProjectInfo;

        private static IUnityProjectExporter exporter = null;

        private static IUnityProjectExporter Exporter => exporter ?? (exporter = new TemplatedUnityProjectExporter(new DirectoryInfo(Utilities.MSBuildProjectFolder),
            TemplateFiles.Instance.MSBuildSolutionTemplatePath,
            TemplateFiles.Instance.SDKProjectFileTemplatePath,
            TemplateFiles.Instance.SDKGeneratedProjectFileTemplatePath,
            TemplateFiles.Instance.SDKProjectPropsFileTemplatePath,
            TemplateFiles.Instance.SDKProjectTargetsFileTemplatePath,
            TemplateFiles.Instance.MSBuildForUnityCommonPropsTemplatePath,
            TemplateFiles.Instance.DependenciesProjectTemplatePath,
            TemplateFiles.Instance.DependenciesPropsTemplatePath,
            TemplateFiles.Instance.DependenciesTargetsTemplatePath));

        public static MSBuildToolsConfig Config { get; } = MSBuildToolsConfig.Load();

        [MenuItem(FullGeneration, priority = 101)]
        public static void ToggleAutoGenerate()
        {
            Config.FullGenerationEnabled = !Config.FullGenerationEnabled;
            Menu.SetChecked(FullGeneration, Config.FullGenerationEnabled);
            // If we just toggled on, regenerate everything
            RefreshGeneratedOutput(forceGenerateEverything: true, forceCompleteGeneration: false);
        }

        [MenuItem(FullGeneration, true, priority = 101)]
        public static bool ToggleAutoGenerate_Validate()
        {
            Menu.SetChecked(FullGeneration, Config.FullGenerationEnabled);
            return true;
        }


        [MenuItem("MSBuild/Regenerate C# SDK Projects", priority = 102)]
        public static void GenerateSDKProjects()
        {
            try
            {
                RefreshGeneratedOutput(forceGenerateEverything: true, forceCompleteGeneration: false);
                Debug.Log($"{nameof(GenerateSDKProjects)} Completed Succesfully.");
            }
            catch
            {
                Debug.LogError($"{nameof(GenerateSDKProjects)} Failed.");
                throw;
            }
        }

        public static void RegenerateSDKProjects()
        {
            RegenerateEverything(unityProjectInfo = new UnityProjectInfo(Debug.unityLogger, SupportedBuildTargets, Config, performCompleteParse: true), completeGeneration: true);
            Debug.Log($"{nameof(RegenerateSDKProjects)} Completed Succesfully.");
        }

        [MenuItem("MSBuild/Documentation...", priority = 203)]
        public static void LaunchHelp()
        {
            Process.Start("https://github.com/microsoft/MSBuildForUnity");
        }

        static MSBuildTools()
        {
            if (EditorAnalyticsSessionInfo.elapsedTime == 0)
            {
                // The Unity asset database cannot be queried until the Editor is fully loaded. The first editor update tick seems to be a safe bet for this.

                // Ensure a single invocation
                EditorApplication.update -= OnUpdate;
                EditorApplication.update += OnUpdate;
                void OnUpdate()
                {
                    EditorApplication.update -= OnUpdate;
                    RefreshGeneratedOutput(forceGenerateEverything: false, forceCompleteGeneration: false);
                }
            }
            else
            {
                RefreshGeneratedOutput(forceGenerateEverything: false, forceCompleteGeneration: false);
            }
        }

        private static void RefreshGeneratedOutput(bool forceGenerateEverything, bool forceCompleteGeneration)
        {
            // In this method, the following must happen
            // - Clean up builds if necessary
            // - Generate the common props file if necessary
            // - Regenerate everything else if necessary
            // - Build if the clean was done

            BuildTarget currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            ApiCompatibilityLevel targetFramework = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup);

            bool buildTargetOrFrameworkChanged = EditorPrefs.GetInt($"{nameof(MSBuildTools)}.{nameof(currentBuildTarget)}") != (int)currentBuildTarget
                || EditorPrefs.GetInt($"{nameof(MSBuildTools)}.{nameof(targetFramework)}") != (int)targetFramework
                || forceGenerateEverything;

            if (buildTargetOrFrameworkChanged || CSProjectAssetRemoved)
            {
                // We clean up previous build if the EditorPrefs currentBuildTarget or targetFramework is different from current ones.
                // Or a CS Project file has been removed and we need to clean up
                MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.CleanProfileName);
            }

            // Get the token file in the Unity Temp directory, if it exists.
            Version tokenVerison = GetCurrentTokenVersion();

            // We regenerate, if the token file exists, and it's current version.
            bool doesCurrentVersionTokenFileExist = tokenVerison != null && tokenVerison == MSBuildForUnityVersion;

            // We perform the regeneration of complete or partial pass in the following cases:
            // - forceGenerateEverything is true (we are told to)
            // - buildTargetOrFrameworkChanged is true (target framework changed)
            // - doesCurrentVersionTokenFileExist is false (version changed, or editor just opened)
            // - CSProjectAssetChanged is true (a csproj file has been added)
            // - AssemblyDefinitionAssetChanged is true and Config.FullGenerationEnabled is true (asmdef change and full regen is enabled that will gen based off asmdefs)

            // - AutoGenerateEnabled and token file doesn't exist or shouldClean is true
            bool performRegeneration = forceGenerateEverything || buildTargetOrFrameworkChanged || !doesCurrentVersionTokenFileExist || CSProjectAssetChanged
                || (AssemblyDefinitionAssetChanged && Config.FullGenerationEnabled);

            // Reset the values after using them
            CSProjectAssetChanged = false;
            AssemblyDefinitionAssetChanged = false;
            CSProjectAssetRemoved = false;

            if (performRegeneration || unityProjectInfo == null)
            {
                // Create the project info only if it's null or we need to regenerate
                unityProjectInfo = new UnityProjectInfo(Debug.unityLogger, SupportedBuildTargets, Config, Config.FullGenerationEnabled || forceCompleteGeneration);
            }

            if (performRegeneration)
            {
                // If we are forced complete, then we regenerate, otherwise perform the one that is selected
                RegenerateEverything(unityProjectInfo, Config.FullGenerationEnabled || forceCompleteGeneration);
            }

            if (!doesCurrentVersionTokenFileExist)
            {
                foreach (string tokenFile in Directory.GetFiles(Path.Combine(Utilities.ProjectPath, "Temp"), "*_token.msb4u", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(tokenFile);
                }

                File.Create(Path.Combine(Utilities.ProjectPath, "Temp", $"{MSBuildForUnityVersion.ToString(3)}_token.msb4u"))
                    .Dispose();
            }

            // Write the current targetframework and build target
            EditorPrefs.SetInt($"{nameof(MSBuildTools)}.{nameof(currentBuildTarget)}", (int)currentBuildTarget);
            EditorPrefs.SetInt($"{nameof(MSBuildTools)}.{nameof(targetFramework)}", (int)targetFramework);

            // If we cleaned, now build, or if we regenerated
            if (buildTargetOrFrameworkChanged || performRegeneration)
            {
                MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.BuildProfileName);
            }
        }

        private static Version GetCurrentTokenVersion()
        {
            string[] file = Directory.GetFiles(Path.Combine(Utilities.ProjectPath, "Temp"), "*_token.msb4u", SearchOption.TopDirectoryOnly);

            if (file.Length > 0)
            {
                string versionNumber = Path.GetFileNameWithoutExtension(file[0]).Split('_')[0];

                string[] versionParts = versionNumber.Split('.');

                if (versionParts.Length == 3
                    && int.TryParse(versionParts[0], out int major)
                    && int.TryParse(versionParts[1], out int minor)
                    && int.TryParse(versionParts[2], out int patch))
                {
                    return new Version(major, minor, patch);
                }
            }

            return null;
        }

        private static void ExportCoreUnityPropFiles(UnityProjectInfo unityProjectInfo)
        {
            foreach (CompilationPlatformInfo platform in unityProjectInfo.AvailablePlatforms)
            {
                // Check for specialized template, otherwise get the common one
                MSBuildUnityProjectExporter.ExportCoreUnityPropFile(Exporter, platform, true);
                MSBuildUnityProjectExporter.ExportCoreUnityPropFile(Exporter, platform, false);
            }

            MSBuildUnityProjectExporter.ExportCoreUnityPropFile(Exporter, unityProjectInfo.EditorPlatform, true);
        }


        private static void RegenerateEverything(UnityProjectInfo unityProjectInfo, bool completeGeneration)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            long postCleanupAndCopyStamp = 0, solutionExportStart = 0, solutionExportEnd = 0, exporterStart = 0, exporterEnd = 0, propsFileGenerationStart = 0, propsFileGenerationEnd = 0;
            try
            {
                if (Directory.Exists(Utilities.MSBuildProjectFolder))
                {
                    // Create a copy of the packages as they might change after we create the MSBuild project
                    foreach (string file in Directory.EnumerateFiles(Utilities.MSBuildProjectFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                }
                else
                {
                    Directory.CreateDirectory(Utilities.MSBuildProjectFolder);
                }

                postCleanupAndCopyStamp = stopwatch.ElapsedMilliseconds;

                propsFileGenerationStart = stopwatch.ElapsedMilliseconds;
                MSBuildUnityProjectExporter.ExportCommonPropsFile(Exporter, MSBuildForUnityVersion, unityProjectInfo.CurrentPlayerPlatform);
                if (completeGeneration)
                {
                    ExportCoreUnityPropFiles(unityProjectInfo);
                }
                propsFileGenerationEnd = stopwatch.ElapsedMilliseconds;

                solutionExportStart = stopwatch.ElapsedMilliseconds;
                if (completeGeneration)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(Utilities.MSBuildProjectFolder);
                    unityProjectInfo.ExportSolution(Exporter, new FileInfo(Exporter.GetSolutionFilePath(unityProjectInfo)), directoryInfo);
                    unityProjectInfo.ExportProjects(Exporter, directoryInfo);
                }
                MSBuildUnityProjectExporter.ExportTopLevelDependenciesProject(Exporter, MSBuildForUnityVersion, Config, new DirectoryInfo(Utilities.MSBuildProjectFolder), unityProjectInfo);
                solutionExportEnd = stopwatch.ElapsedMilliseconds;


                string nuGetConfigPath = Path.Combine(Utilities.AssetPath, Path.GetFileNameWithoutExtension(TemplateFiles.Instance.NuGetConfigPath));
                
                // Copy the NuGet.config file if it does not exist
                if (!File.Exists(nuGetConfigPath))
                {
                    File.Copy(TemplateFiles.Instance.NuGetConfigPath, nuGetConfigPath);
                }

                foreach (string otherFile in TemplateFiles.Instance.OtherFiles)
                {
                    File.Copy(otherFile, Path.Combine(Utilities.MSBuildProjectFolder, Path.GetFileName(otherFile)));
                }

                if (completeGeneration)
                {
                    string buildProjectsFile = "BuildProjects.proj";
                    if (!File.Exists(Path.Combine(Utilities.MSBuildOutputFolder, buildProjectsFile)))
                    {
                        GenerateBuildProjectsFile(buildProjectsFile, Exporter.GetSolutionFilePath(unityProjectInfo), unityProjectInfo.AvailablePlatforms);
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
                Debug.Log($"Whole Generate Projects process took {stopwatch.ElapsedMilliseconds} ms; actual generation took {stopwatch.ElapsedMilliseconds - postCleanupAndCopyStamp}; solution export: {solutionExportEnd - solutionExportStart}; exporter creation: {exporterEnd - exporterStart}; props file generation: {propsFileGenerationEnd - propsFileGenerationStart}");
            }
        }

        private static void GenerateBuildProjectsFile(string fileName, string solutionPath, IEnumerable<CompilationPlatformInfo> compilationPlatforms)
        {
            string template = File.ReadAllText(TemplateFiles.Instance.BuildProjectsTemplatePath);
            if (!Utilities.TryGetXMLTemplate(template, "PLATFORM_TARGET", out string platformTargetTemplate))
            {
                Debug.LogError($"Corrupt template for BuildProjects.proj file.");
                return;
            }

            List<string> batBuildEntry = new List<string>();
            List<string> entries = new List<string>();
            foreach (CompilationPlatformInfo platform in compilationPlatforms)
            {
                // Add one for InEditor
                entries.Add(Utilities.ReplaceTokens(platformTargetTemplate, new Dictionary<string, string>()
                {
                    {"##PLATFORM_TOKEN##", platform.Name },
                    {"##CONFIGURATION_TOKEN##", "InEditor" }
                }));

                //Add one for Player, except WSA special case
                if (platform.BuildTarget != BuildTarget.WSAPlayer)
                {
                    entries.Add(Utilities.ReplaceTokens(platformTargetTemplate, new Dictionary<string, string>()
                    {
                        {"##PLATFORM_TOKEN##", platform.Name },
                        {"##CONFIGURATION_TOKEN##", "Player" }
                    }));
                }

                batBuildEntry.Add($"dotnet msbuild {fileName} /t:Build{platform.Name}InEditor");
                batBuildEntry.Add($"dotnet msbuild {fileName} /t:Build{platform.Name}Player");
            }

            string output = Utilities.ReplaceTokens(template, new Dictionary<string, string>()
            {
                {platformTargetTemplate, string.Join("\n", entries) },
                {"<!--TARGET_PROJECT_PATH_TOKEN-->", solutionPath }
            });

            File.WriteAllText(Path.Combine(Utilities.MSBuildOutputFolder, fileName), output);
            File.WriteAllText(Path.Combine(Utilities.MSBuildOutputFolder, "BuildAll.bat"), string.Join("\r\n", batBuildEntry));
        }
    }
}
#endif
