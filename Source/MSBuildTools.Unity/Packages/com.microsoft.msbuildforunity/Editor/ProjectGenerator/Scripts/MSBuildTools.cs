// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

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
            // Create a copy of the packages as they might change after we create the MSBuild project
            string generatedProjectPath = Path.Combine(Utilities.MSBuildOutputFolder, "Projects");
            try
            {
                Utilities.EnsureCleanDirectory(generatedProjectPath);
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

            MakePackagesCopy(Utilities.MSBuildOutputFolder);

            List<CompilationPlatformInfo> platforms = CompilationPipeline.GetAssemblyDefinitionPlatforms()
                .Where(t => supportedBuildTargets.Contains(t.BuildTarget))
                .Select(CompilationPlatformInfo.GetCompilationPlatform)
                .OrderBy(t => t.Name)
                .ToList();

            CompilationPlatformInfo editorPlatform = CompilationPlatformInfo.GetEditorPlatform();

            CreateCommonPropsFile(platforms, editorPlatform, generatedProjectPath);
            UnityProjectInfo unityProjectInfo = new UnityProjectInfo(platforms, generatedProjectPath);

            IProjectExporter exporter = new TemplatedProjectExporter(new DirectoryInfo(generatedProjectPath), TemplateFiles.Instance.MSBuildSolutionTemplatePath, TemplateFiles.Instance.SDKProjectFileTemplatePath);
            exporter.ExportSolution(unityProjectInfo);

            foreach (string otherFile in TemplateFiles.Instance.OtherFiles)
            {
                File.Copy(otherFile, Path.Combine(generatedProjectPath, Path.GetFileName(otherFile)));
            }

            string buildProjectsFile = "BuildProjects.proj";
            if (!File.Exists(Path.Combine(Utilities.MSBuildOutputFolder, buildProjectsFile)))
            {
                GenerateBuildProjectsFile(buildProjectsFile, unityProjectInfo.UnityProjectName, platforms);
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

        private static void MakePackagesCopy(string msbuildFolder)
        {
            string packageCache = Path.Combine(Application.dataPath, "..", "Library/PackageCache");
            string[] directories = Directory.GetDirectories(packageCache);

            string outputDirectory = Path.Combine(msbuildFolder, Utilities.PackagesCopyFolderName);
            Utilities.EnsureCleanDirectory(outputDirectory);

            foreach (string directory in directories)
            {
                Utilities.CopyDirectory(directory, Path.Combine(outputDirectory, Path.GetFileName(directory).Split('@')[0]), ".asmdef", ".asmdef.meta", ".cs", ".cs.meta", ".template", ".template.meta", ".dll", ".dll.meta");
            }
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

            string platformTemplate = File.ReadAllText(TemplateFiles.Instance.GetTemplateFilePathForPlatform(platform.Name, configuration, platform.ScriptingBackend));

            string platformPropsText;
            if (inEditorConfiguration)
            {
                platformPropsText = ProcessPlatformTemplate(platformTemplate, platform.Name, configuration, platform.BuildTarget, platform.TargetFramework,
                    platform.CommonPlatformReferences.Concat(platform.AdditionalInEditorReferences), platform.CommonPlatformDefines.Concat(platform.AdditionalInEditorDefines));
            }
            else
            {
                platformPropsText = ProcessPlatformTemplate(platformTemplate, platform.Name, configuration, platform.BuildTarget, platform.TargetFramework,
                    platform.CommonPlatformReferences.Concat(platform.AdditionalPlayerReferences), platform.CommonPlatformDefines.Concat(platform.AdditionalPlayerDefines));
            }

            File.WriteAllText(Path.Combine(projectOutputFolder, $"{platform.Name}.{configuration}.props"), platformPropsText);
        }

        private static string ProcessPlatformTemplate(string platformTemplate, string platformName, string configuration, BuildTarget buildTarget, TargetFramework targetFramework, IEnumerable<string> references, IEnumerable<string> defines, params HashSet<string>[] priorToCheck)
        {
            if (Utilities.TryGetXMLTemplate(platformTemplate, "PLATFORM_COMMON_REFERENCE", out string platformCommonReferenceTemplate))
            {
                ProcessReferences(buildTarget, references, out HashSet<string> platformAssemblySearchPaths, out HashSet<string> platformAssemblyReferencePaths, priorToCheck);

                string targetUWPPlatform = uwpTargetPlatformVersion;
                if (string.IsNullOrWhiteSpace(targetUWPPlatform))
                {
                    targetUWPPlatform = Utilities.GetUWPSDKs().Max().ToString(4);
                }

                string minUWPPlatform = uwpMinPlatformVersion;
                if (string.IsNullOrWhiteSpace(minUWPPlatform) || new Version(minUWPPlatform) < DefaultMinUWPSDK)
                {
                    minUWPPlatform = DefaultMinUWPSDK.ToString();
                }
                string[] versionParts = Application.unityVersion.Split('.');
                Dictionary<string, string> platformTokens = new Dictionary<string, string>()
                {
                    {"<!--TARGET_FRAMEWORK_TOKEN-->", targetFramework.AsMSBuildString() },
                    {"<!--PLATFORM_COMMON_DEFINE_CONSTANTS-->", string.Join(";", defines) },
                    {"<!--PLATFORM_COMMON_ASSEMBLY_SEARCH_PATHS_TOKEN-->", string.Join(";", platformAssemblySearchPaths)},

                    // These are UWP specific, but they will be no-op if not needed
                    { "<!--UWP_TARGET_PLATFORM_VERSION_TOKEN-->", targetUWPPlatform },
                    { "<!--UWP_MIN_PLATFORM_VERSION_TOKEN-->", minUWPPlatform },

                    { "<!--UNITY_MAJOR_VERSION_TOKEN-->", versionParts[0] },
                    { "<!--UNITY_MINOR_VERSION_TOKEN-->", versionParts[1] }
                };

                platformTokens.Add(platformCommonReferenceTemplate, string.Join("\r\n", GetReferenceEntries(platformCommonReferenceTemplate, platformAssemblyReferencePaths)));

                return Utilities.ReplaceTokens(platformTemplate, platformTokens);
            }
            else
            {
                Debug.LogError($"Invalid platform template format for '{platformName}' with configuration '{configuration}'");
                return platformTemplate;
            }
        }

        public static IEnumerable<string> GetReferenceEntries(string template, IEnumerable<string> references)
        {
            return references.Select(t => Utilities.ReplaceTokens(template, new Dictionary<string, string>()
            {
                { "##REFERENCE_TOKEN##", Path.GetFileNameWithoutExtension(t) },
                { "<!--HINT_PATH_TOKEN-->", t }
            }));
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