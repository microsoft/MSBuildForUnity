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

        public string UnityMajorVersion { get; set; }

        public string UnityMinorVersion { get; set; }

        public string CurrentUnityPlatform { get; set; }

        public string CurrentTargetFramework { get; set; }

        public DirectoryInfo UnityProjectAssetsDirectory { get; set; }

        public DirectoryInfo GeneratedProjectOutputPath { get; set; }

        internal TemplatedCommonPropsExporter(FileTemplate fileTemplate, FileInfo exportPath)
            : base(fileTemplate, exportPath)
        {
        }

        protected override void Export(TemplatedWriter writer)
        {
            writer.Write(UnityMajorVersionToken, UnityMajorVersion);
            writer.Write(UnityMinorVersionToken, UnityMinorVersion);
            writer.Write(CurrentUnityPlatformToken, CurrentUnityPlatform);
            writer.Write(CurrentTargetFrameworkToken, CurrentTargetFramework);
            writer.Write(UnityProjectAssetsDirectoryToken, UnityProjectAssetsDirectory.FullName);
            writer.Write(GeneratedOutputDirectoryToken, GeneratedProjectOutputPath.FullName);
        }
    }
}
#endif
