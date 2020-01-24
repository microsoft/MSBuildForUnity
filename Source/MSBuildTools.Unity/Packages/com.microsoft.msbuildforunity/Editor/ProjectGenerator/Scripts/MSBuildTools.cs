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
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    public class MSBuildToolsConfig
    {
        private const int CurrentConfigVersion = 3;

        private static string MSBuildSettingsFilePath { get; } = Path.Combine(Utilities.ProjectPath, "MSBuild", "settings.json");

        [SerializeField]
        private int version = 0;

        [SerializeField]
        private bool autoGenerateEnabled = false;

        [SerializeField]
        private string dependenciesProjectGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpEditorGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpFirstPassGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpFirstPassEditorGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string builtInPackagesFolderGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string importedPackagesFolderGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string externalPackagesFolderGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string solutionGuid = Guid.NewGuid().ToString();

        public bool AutoGenerateEnabled
        {
            get => autoGenerateEnabled;
            set
            {
                autoGenerateEnabled = value;
                Save();
            }
        }

        internal Guid DependenciesProjectGuid { get; private set; }

        internal Guid AssemblyCSharpGuid { get; private set; }

        internal Guid AssemblyCSharpEditorGuid { get; private set; }

        internal Guid AssemblyCSharpFirstPassGuid { get; private set; }

        internal Guid AssemblyCSharpFirstPassEditorGuid { get; private set; }

        internal Guid BuiltInPackagesFolderGuid { get; private set; }

        internal Guid ImportedPackagesFolderGuid { get; private set; }

        internal Guid ExternalPackagesFolderGuid { get; private set; }

        internal Guid SolutionGuid { get; private set; }

        private void Save()
        {
            // Ensure directory exists first
            Directory.CreateDirectory(Path.GetDirectoryName(MSBuildSettingsFilePath));
            File.WriteAllText(MSBuildSettingsFilePath, EditorJsonUtility.ToJson(this));
        }

        public static MSBuildToolsConfig Load()
        {
            MSBuildToolsConfig toReturn = new MSBuildToolsConfig();

            if (File.Exists(MSBuildSettingsFilePath))
            {
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(MSBuildSettingsFilePath), toReturn);
            }

            bool needToSave = false;

            toReturn.DependenciesProjectGuid = EnsureGuid(ref toReturn.dependenciesProjectGuid, ref needToSave);

            toReturn.AssemblyCSharpGuid = EnsureGuid(ref toReturn.assemblyCSharpGuid, ref needToSave);
            toReturn.AssemblyCSharpEditorGuid = EnsureGuid(ref toReturn.assemblyCSharpEditorGuid, ref needToSave);
            toReturn.AssemblyCSharpFirstPassGuid = EnsureGuid(ref toReturn.assemblyCSharpFirstPassGuid, ref needToSave);
            toReturn.AssemblyCSharpFirstPassEditorGuid = EnsureGuid(ref toReturn.assemblyCSharpFirstPassEditorGuid, ref needToSave);

            toReturn.BuiltInPackagesFolderGuid = EnsureGuid(ref toReturn.builtInPackagesFolderGuid, ref needToSave);
            toReturn.ImportedPackagesFolderGuid = EnsureGuid(ref toReturn.importedPackagesFolderGuid, ref needToSave);
            toReturn.ExternalPackagesFolderGuid = EnsureGuid(ref toReturn.externalPackagesFolderGuid, ref needToSave);

            toReturn.SolutionGuid = EnsureGuid(ref toReturn.solutionGuid, ref needToSave);

            if (CurrentConfigVersion > toReturn.version)
            {
                toReturn.version = CurrentConfigVersion;
                needToSave = true;
            }

            if (needToSave)
            {
                toReturn.Save();
            }

            return toReturn;
        }

        private static Guid EnsureGuid(ref string field, ref bool needToSave)
        {
            if (!Guid.TryParse(field, out Guid guid))
            {
                guid = Guid.NewGuid();
                field = guid.ToString();

                needToSave = true;
            }

            return guid;
        }
    }

    /// <summary>
    /// Class that exposes the MSBuild project generation operation.
    /// </summary>
    [InitializeOnLoad]
    public static class MSBuildTools
    {
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
        public const string AutoGenerate = "MSBuild/Auto Generation Enabled";

        private static readonly string TokenFilePath = Path.Combine(Utilities.ProjectPath, "Temp", "PropsGeneratedThisEditorInstance.token");
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

        [MenuItem(AutoGenerate, priority = 101)]
        public static void ToggleAutoGenerate()
        {
            Config.AutoGenerateEnabled = !Config.AutoGenerateEnabled;
            Menu.SetChecked(AutoGenerate, Config.AutoGenerateEnabled);
            // If we just toggled on, regenerate everything
            RefreshGeneratedOutput(forceGenerateEverything: true, forceCompleteGeneration: false);
        }

        [MenuItem(AutoGenerate, true, priority = 101)]
        public static bool ToggleAutoGenerate_Validate()
        {
            Menu.SetChecked(AutoGenerate, Config.AutoGenerateEnabled);
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
            RegenerateEverything(unityProjectInfo = new UnityProjectInfo(SupportedBuildTargets, Config, performCompleteParse: true), completeGeneration: true);
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

            bool shouldClean = EditorPrefs.GetInt($"{nameof(MSBuildTools)}.{nameof(currentBuildTarget)}") != (int)currentBuildTarget
                || EditorPrefs.GetInt($"{nameof(MSBuildTools)}.{nameof(targetFramework)}") != (int)targetFramework
                || forceGenerateEverything;

            if (shouldClean)
            {
                // We clean up previous build if the EditorPrefs currentBuildTarget or targetFramework is different from current ones.
                MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.CleanProfileName);
            }

            bool doesTokenFileExist = File.Exists(TokenFilePath);

            // We regenerate everything if:
            // - We are forced to
            // - AutoGenerateEnabled and token file doesn't exist or shouldClean is true
            bool regenerateEverything = forceGenerateEverything || (Config.AutoGenerateEnabled && (!doesTokenFileExist || shouldClean));

            if (regenerateEverything || unityProjectInfo == null)
            {
                // Create the project info only if it's null or we need to regenerate
                unityProjectInfo = new UnityProjectInfo(SupportedBuildTargets, Config, Config.AutoGenerateEnabled || forceCompleteGeneration);
            }

            // We regenerate the common "directory" props file under the following conditions:
            // - Token file doesn't exist which means editor was just started
            // - EditorPrefs currentBuildTarget or targetFramework is different from current ones (same as for executing a clean)
            if (shouldClean || !doesTokenFileExist)
            {
                MSBuildUnityProjectExporter.ExportCommonPropsFile(Exporter, unityProjectInfo.CurrentPlayerPlatform);
            }

            if (regenerateEverything)
            {
                RegenerateEverything(unityProjectInfo, Config.AutoGenerateEnabled || forceCompleteGeneration);
            }

            if (!doesTokenFileExist)
            {
                File.Create(TokenFilePath).Dispose();
            }

            // Write the current targetframework and build target
            EditorPrefs.SetInt($"{nameof(MSBuildTools)}.{nameof(currentBuildTarget)}", (int)currentBuildTarget);
            EditorPrefs.SetInt($"{nameof(MSBuildTools)}.{nameof(targetFramework)}", (int)targetFramework);

            // If we cleaned, now build
            if (shouldClean)
            {
                MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.BuildProfileName);
            }
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
                MSBuildUnityProjectExporter.ExportCommonPropsFile(Exporter, unityProjectInfo.CurrentPlayerPlatform);
                if (completeGeneration)
                {
                    ExportCoreUnityPropFiles(unityProjectInfo);
                }
                propsFileGenerationEnd = stopwatch.ElapsedMilliseconds;

                solutionExportStart = stopwatch.ElapsedMilliseconds;
                if (completeGeneration)
                {
                    Exporter.ExportSolution(unityProjectInfo, Config);
                    unityProjectInfo.ExportProjects(Exporter, new DirectoryInfo(Utilities.MSBuildProjectFolder));
                }
                MSBuildUnityProjectExporter.ExportTopLevelDependenciesProject(Exporter, Config, new DirectoryInfo(Utilities.MSBuildProjectFolder), unityProjectInfo);
                solutionExportEnd = stopwatch.ElapsedMilliseconds;

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
