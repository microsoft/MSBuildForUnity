// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Exporters;
using System;
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
        public static void ExportCommonPropsFile(IUnityProjectExporter exporter, CompilationPlatformInfo currentPlayerPlatform)
        {
            ICommonPropsExporter propsExporter = exporter.CreateCommonPropsExporter(new FileInfo(Path.Combine(Utilities.ProjectPath, "MSBuildForUnity.Common.props")));

            string[] versionParts = Application.unityVersion.Split('.');
            propsExporter.UnityMajorVersion = versionParts[0];
            propsExporter.UnityMinorVersion = versionParts[1];

            propsExporter.GeneratedProjectOutputPath = new DirectoryInfo(Utilities.MSBuildOutputFolder);
            propsExporter.UnityProjectAssetsDirectory = new DirectoryInfo(Utilities.AssetPath);

            propsExporter.CurrentTargetFramework = currentPlayerPlatform.TargetFramework.AsMSBuildString();
            propsExporter.CurrentUnityPlatform = currentPlayerPlatform.Name;

            propsExporter.Write();
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
    }
}
#endif