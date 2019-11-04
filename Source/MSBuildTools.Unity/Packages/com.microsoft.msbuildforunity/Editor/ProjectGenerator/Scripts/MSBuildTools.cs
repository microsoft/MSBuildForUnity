// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// Class that exposes the MSBuild project generation operation.
    /// </summary>
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
        public static readonly Version DefaultMinUWPSDK = new Version("10.0.14393.0");

        private static readonly string uwpMinPlatformVersion = EditorUserBuildSettings.wsaMinUWPSDK;
        private static readonly string uwpTargetPlatformVersion = EditorUserBuildSettings.wsaUWPSDK;

        private static IProjectExporter exporter = null;

        private static IProjectExporter Exporter => exporter ?? (exporter = new TemplatedProjectExporter(new DirectoryInfo(Utilities.MSBuildProjectFolder), TemplateFiles.Instance.MSBuildSolutionTemplatePath, TemplateFiles.Instance.SDKProjectFileTemplatePath, TemplateFiles.Instance.SDKProjectPropsFileTemplatePath, TemplateFiles.Instance.SDKProjectTargetsFileTemplatePath));

        [MenuItem("MSBuild/Generate C# SDK Projects", priority = 101)]
        public static void GenerateSDKProjects()
        {
            try
            {
                RunGenerateSDKProjects();
                Debug.Log($"{nameof(GenerateSDKProjects)} Completed Succesfully.");
            }
            catch
            {
                Debug.LogError($"{nameof(GenerateSDKProjects)} Failed.");
                throw;
            }
        }

        private static void RunGenerateSDKProjects()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            long postCleanupAndCopyStamp = 0, solutionExportStart = 0, solutionExportEnd = 0, exporterStart = 0, exporterEnd = 0, propsFileGenerationStart = 0, propsFileGenerationEnd = 0;
            try
            {
                // Create a copy of the packages as they might change after we create the MSBuild project
                try
                {
                    Utilities.EnsureCleanDirectory(Utilities.MSBuildProjectFolder);
                }
                catch (IOException ex)
                {
                    if (ex.Message.Contains(@"db.lock"))
                    {
                        Debug.LogError("Generated project appears to be still open with Visual Studio.");
                        throw new InvalidDataException("Generated project appears to be still open with Visual Studio.", ex);
                    }
                    else
                    {
                        throw;
                    }
                }

                Utilities.EnsureCleanDirectory(Path.Combine(Utilities.MSBuildOutputFolder, "Output"));

                postCleanupAndCopyStamp = stopwatch.ElapsedMilliseconds;

                List<CompilationPlatformInfo> platforms = CompilationPipeline.GetAssemblyDefinitionPlatforms()
                    .Where(t => supportedBuildTargets.Contains(t.BuildTarget))
                    .Select(CompilationPlatformInfo.GetCompilationPlatform)
                    .OrderBy(t => t.Name)
                    .ToList();

                CompilationPlatformInfo editorPlatform = CompilationPlatformInfo.GetEditorPlatform();

                propsFileGenerationStart = stopwatch.ElapsedMilliseconds;
                CreateCommonPropsFile(platforms, editorPlatform, Utilities.MSBuildProjectFolder);
                propsFileGenerationEnd = stopwatch.ElapsedMilliseconds;
                UnityProjectInfo unityProjectInfo = new UnityProjectInfo(platforms);

                solutionExportStart = stopwatch.ElapsedMilliseconds;
                Exporter.ExportSolution(unityProjectInfo);
                solutionExportEnd = stopwatch.ElapsedMilliseconds;

                foreach (string otherFile in TemplateFiles.Instance.OtherFiles)
                {
                    File.Copy(otherFile, Path.Combine(Utilities.MSBuildProjectFolder, Path.GetFileName(otherFile)));
                }

                string buildProjectsFile = "BuildProjects.proj";
                if (!File.Exists(Path.Combine(Utilities.MSBuildOutputFolder, buildProjectsFile)))
                {
                    GenerateBuildProjectsFile(buildProjectsFile, unityProjectInfo.UnityProjectName, platforms);
                }
            }
            finally
            {
                stopwatch.Stop();
                Debug.Log($"Whole Generate Projects process took {stopwatch.ElapsedMilliseconds} ms; actual generation took {stopwatch.ElapsedMilliseconds - postCleanupAndCopyStamp}; solution export: {solutionExportEnd - solutionExportStart}; exporter creation: {exporterEnd - exporterStart}; props file generation: {propsFileGenerationEnd - propsFileGenerationStart}");
            }
        }

        private static void GenerateBuildProjectsFile(string fileName, string projectName, IEnumerable<CompilationPlatformInfo> compilationPlatforms)
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
                {"<!--TARGET_PROJECT_NAME_TOKEN-->", projectName }
            });

            File.WriteAllText(Path.Combine(Utilities.MSBuildOutputFolder, fileName), output);
            File.WriteAllText(Path.Combine(Utilities.MSBuildOutputFolder, "BuildAll.bat"), string.Join("\r\n", batBuildEntry));
        }

        private static void CreateCommonPropsFile(IEnumerable<CompilationPlatformInfo> availablePlatforms, CompilationPlatformInfo editorPlatform, string projectOutputFolder)
        {
            foreach (CompilationPlatformInfo platform in availablePlatforms)
            {
                // Check for specialized template, otherwise get the common one
                ProcessPlatformTemplateForConfiguration(platform, projectOutputFolder, true);
                ProcessPlatformTemplateForConfiguration(platform, projectOutputFolder, false);
            }

            ProcessPlatformTemplateForConfiguration(editorPlatform, projectOutputFolder, true);
        }

        private static void ProcessPlatformTemplateForConfiguration(CompilationPlatformInfo platform, string projectOutputFolder, bool inEditorConfiguration)
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

            fileTemplate.Write(Path.Combine(projectOutputFolder, $"{platform.Name}.{configuration}.props"), rootReplacementSet);
        }

        private static void ProcessPlatformTemplate(ITemplatePart rootPart, TemplateReplacementSet rootReplacementSet, string platformName, string configuration, BuildTarget buildTarget, TargetFramework targetFramework, IEnumerable<string> references, IEnumerable<string> defines, params HashSet<string>[] priorToCheck)
        {
            ProcessReferences(buildTarget, references, out HashSet<string> platformAssemblySearchPaths, out HashSet<string> platformAssemblyReferencePaths, priorToCheck);

            string minUWPPlatform = uwpMinPlatformVersion;
            if (string.IsNullOrWhiteSpace(minUWPPlatform) || new Version(minUWPPlatform) < DefaultMinUWPSDK)
            {
                minUWPPlatform = DefaultMinUWPSDK.ToString();
            }

            string[] versionParts = Application.unityVersion.Split('.');
            // This is a try replace because some may hardcode this value
            rootPart.TryReplaceToken("TARGET_FRAMEWORK", rootReplacementSet, targetFramework.AsMSBuildString());

            rootPart.Tokens["PLATFORM_COMMON_DEFINE_CONSTANTS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", defines));
            rootPart.Tokens["PLATFORM_COMMON_ASSEMBLY_SEARCH_PATHS"].AssignValue(rootReplacementSet, new DelimitedStringSet(";", platformAssemblySearchPaths));

            // These are UWP specific, but they will be no-op if not needed
            if (buildTarget == BuildTarget.WSAPlayer && configuration == "Player")
            {
                string targetUWPPlatform = uwpTargetPlatformVersion;
                if (string.IsNullOrWhiteSpace(targetUWPPlatform))
                {
                    targetUWPPlatform = Utilities.GetUWPSDKs().Max().ToString(4);
                }
                rootPart.TryReplaceToken("UWP_TARGET_PLATFORM_VERSION", rootReplacementSet, targetUWPPlatform);
                rootPart.TryReplaceToken("UWP_MIN_PLATFORM_VERSION", rootReplacementSet, minUWPPlatform);
            }

            rootPart.Tokens["UNITY_MAJOR_VERSION"].AssignValue(rootReplacementSet, versionParts[0]);
            rootPart.Tokens["UNITY_MINOR_VERSION"].AssignValue(rootReplacementSet, versionParts[1]);

            ITemplatePart platformCommonReferencePart = rootPart.Templates["PLATFORM_COMMON_REFERENCE"];
            foreach (string reference in platformAssemblyReferencePaths)
            {
                TemplateReplacementSet replacementSet = platformCommonReferencePart.CreateReplacementSet(rootReplacementSet);
                platformCommonReferencePart.Tokens["REFERENCE"].AssignValue(replacementSet, Path.GetFileNameWithoutExtension(reference));
                platformCommonReferencePart.Tokens["HINT_PATH"].AssignValue(replacementSet, reference);
            }
        }

        private static void ProcessReferences(BuildTarget buildTarget, IEnumerable<string> references, out HashSet<string> searchPaths, out HashSet<string> referenceNames, params HashSet<string>[] priorToCheck)
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