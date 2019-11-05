﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    public class MSBuildToolsConfig
    {
        private static string MSBuildSettingsFilePath { get; } = Path.Combine(Utilities.ProjectPath, "MSBuild", "settings.json");

        [SerializeField]
        private bool autoGenerateEnabled = false;

        public bool AutoGenerateEnabled
        {
            get => autoGenerateEnabled;
            set
            {
                autoGenerateEnabled = value;
                Save();
            }
        }

        private void Save()
        {
            File.WriteAllText(MSBuildSettingsFilePath, EditorJsonUtility.ToJson(this));
        }

        public static MSBuildToolsConfig Load()
        {
            MSBuildToolsConfig toReturn = new MSBuildToolsConfig();

            if (File.Exists(MSBuildSettingsFilePath))
            {
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(MSBuildSettingsFilePath), toReturn);
            }

            return toReturn;
        }
    }

    /// <summary>
    /// Class that exposes the MSBuild project generation operation.
    /// </summary>
    [InitializeOnLoad]
    public static class MSBuildTools
    {
        private static readonly HashSet<BuildTarget> supportedBuildTargets = new HashSet<BuildTarget>()
        {
            BuildTarget.StandaloneWindows,
            BuildTarget.StandaloneWindows64,
            BuildTarget.iOS,
            BuildTarget.Android,
            BuildTarget.WSAPlayer
        };

        public const string CSharpVersion = "7.3";
        public const string AutoGenerate = "MSBuild/Generation Enabled";
        public const string AutoGenerateSDKProjects = "MSBuild/Auto-Generate C# SDK Project Files";

        public static readonly Version DefaultMinUWPSDK = new Version("10.0.14393.0");

        private static UnityProjectInfo unityProjectInfo;

        public static UnityProjectInfo UnityProjectInfo => unityProjectInfo ?? (unityProjectInfo = new UnityProjectInfo(supportedBuildTargets));

        private static IProjectExporter exporter = null;

        private static IProjectExporter Exporter => exporter ?? (exporter = new TemplatedProjectExporter(new DirectoryInfo(Utilities.MSBuildProjectFolder),
            TemplateFiles.Instance.MSBuildSolutionTemplatePath,
            TemplateFiles.Instance.SDKProjectFileTemplatePath,
            TemplateFiles.Instance.SDKGeneratedProjectFileTemplatePath,
            TemplateFiles.Instance.SDKProjectPropsFileTemplatePath,
            TemplateFiles.Instance.SDKProjectTargetsFileTemplatePath,
            TemplateFiles.Instance.MSBuildForUnityCommonPropsTemplatePath));

        public static MSBuildToolsConfig Config { get; } = MSBuildToolsConfig.Load();
        
        [MenuItem(AutoGenerate, priority = 101)]
        public static void ToggleAutoGenerate()
        {
            Config.AutoGenerateEnabled = !Config.AutoGenerateEnabled;
            Menu.SetChecked(AutoGenerate, Config.AutoGenerateEnabled);
            RunCoreAutoGenerate();
        }

       
        [MenuItem("MSBuild/Regenerate C# SDK Projects", priority = 102)]
        public static void GenerateSDKProjects()
        {
            try
            {
                RegenerateEverything(true);
                Debug.Log($"{nameof(GenerateSDKProjects)} Completed Succesfully.");
            }
            catch
            {
                Debug.LogError($"{nameof(GenerateSDKProjects)} Failed.");
                throw;
            }
        }

        static MSBuildTools()
        {
            Menu.SetChecked(AutoGenerate, Config.AutoGenerateEnabled);
            RunCoreAutoGenerate();
        }

        private static void RunCoreAutoGenerate()
        {
            Exporter.GenerateDirectoryPropsFile(UnityProjectInfo);

            if (!Config.AutoGenerateEnabled)
            {
                return;
            }

            string tokenFile = Path.Combine(Utilities.ProjectPath, "Temp", "PropsGeneratedThisEditorInstance.token");
            // Check if a file exists, if it does, we already generated this editor instance
            if (!File.Exists(tokenFile))
            {
                RegenerateEverything(false);
                
                File.Create(tokenFile).Dispose();
            }

        }

        private static void ExportCoreUnityPropFiles()
        {
            foreach (CompilationPlatformInfo platform in UnityProjectInfo.AvailablePlatforms)
            {
                // Check for specialized template, otherwise get the common one
                Exporter.ExportCommonPropsFile(platform, true);
                Exporter.ExportCommonPropsFile(platform, false);
            }

            Exporter.ExportCommonPropsFile(UnityProjectInfo.EditorPlatform, true);
        }

        private static void RegenerateEverything(bool reparseUnityData)
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

                // Utilities.EnsureCleanDirectory(Path.Combine(Utilities.MSBuildOutputFolder, "Output"));
                if (reparseUnityData)
                {
                    unityProjectInfo?.Dispose();
                    unityProjectInfo = null;
                }

                postCleanupAndCopyStamp = stopwatch.ElapsedMilliseconds;

                propsFileGenerationStart = stopwatch.ElapsedMilliseconds;
                Exporter.GenerateDirectoryPropsFile(UnityProjectInfo);
                ExportCoreUnityPropFiles();
                propsFileGenerationEnd = stopwatch.ElapsedMilliseconds;

                solutionExportStart = stopwatch.ElapsedMilliseconds;
                RegenerateSolution();
                solutionExportEnd = stopwatch.ElapsedMilliseconds;

                foreach (string otherFile in TemplateFiles.Instance.OtherFiles)
                {
                    File.Copy(otherFile, Path.Combine(Utilities.MSBuildProjectFolder, Path.GetFileName(otherFile)));
                }

                string buildProjectsFile = "BuildProjects.proj";
                if (!File.Exists(Path.Combine(Utilities.MSBuildOutputFolder, buildProjectsFile)))
                {
                    GenerateBuildProjectsFile(buildProjectsFile, Exporter.GetSolutionFilePath(UnityProjectInfo), UnityProjectInfo.AvailablePlatforms);
                }
            }
            finally
            {
                stopwatch.Stop();
                Debug.Log($"Whole Generate Projects process took {stopwatch.ElapsedMilliseconds} ms; actual generation took {stopwatch.ElapsedMilliseconds - postCleanupAndCopyStamp}; solution export: {solutionExportEnd - solutionExportStart}; exporter creation: {exporterEnd - exporterStart}; props file generation: {propsFileGenerationEnd - propsFileGenerationStart}");
            }
        }

        private static void RegenerateSolution()
        {
            Exporter.ExportSolution(UnityProjectInfo);
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