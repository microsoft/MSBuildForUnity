// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    internal class TemplatedCommonPropsExporter : TemplatedExporterBase, ICommonPropsExporter
    {
        private const string UnityMajorVersionToken = "UNITY_MAJOR_VERSION";
        private const string UnityMinorVersionToken = "UNITY_MINOR_VERSION";
        private const string CurrentUnityPlatformToken = "CURRENT_UNITY_PLATFORM";
        private const string CurrentTargetFrameworkToken = "CURRENT_TARGET_FRAMEWORK";
        private const string GeneratedOutputDirectoryToken = "GENERATED_OUTPUT_DIRECTORY";
        private const string UnityProjectAssetsDirectoryToken = "UNITY_PROJECT_ASSETS_PATH";

        private string unityMajorVersion;
        private string unityMinorVersion;
        private string currentUnityPlatform;
        private string currentTargetFramework;
        private DirectoryInfo unityProjectAssetsDirectory;
        private DirectoryInfo generatedProjectOutputPath;

        public string UnityMajorVersion
        {
            get => unityMajorVersion;
            set => UpdateToken(ref unityMajorVersion, value, UnityMajorVersionToken);
        }

        public string UnityMinorVersion
        {
            get => unityMinorVersion;
            set => UpdateToken(ref unityMinorVersion, value, UnityMinorVersionToken);
        }

        public string CurrentUnityPlatform
        {
            get => currentUnityPlatform;
            set => UpdateToken(ref currentUnityPlatform, value, CurrentUnityPlatformToken);
        }

        public string CurrentTargetFramework
        {
            get => currentTargetFramework;
            set => UpdateToken(ref currentTargetFramework, value, CurrentTargetFrameworkToken);
        }

        public DirectoryInfo UnityProjectAssetsDirectory
        {
            get => unityProjectAssetsDirectory;
            set => UpdateToken(ref unityProjectAssetsDirectory, value, UnityProjectAssetsDirectoryToken, t => t.FullName);
        }

        public DirectoryInfo GeneratedProjectOutputPath
        {
            get => generatedProjectOutputPath;
            set => UpdateToken(ref generatedProjectOutputPath, value, GeneratedOutputDirectoryToken, t => t.FullName);
        }

        internal TemplatedCommonPropsExporter(FileTemplate fileTemplate, FileInfo exportPath)
            : base(fileTemplate, exportPath)
        {
        }
    }
}
#endif
