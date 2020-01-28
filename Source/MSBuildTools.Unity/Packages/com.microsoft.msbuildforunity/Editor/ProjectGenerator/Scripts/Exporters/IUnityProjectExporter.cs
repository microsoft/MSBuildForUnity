// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// This interface exposes the APIs for exporting projects.
    /// </summary>
    public interface IUnityProjectExporter
    {
        /// <summary>
        /// Given the <see cref="UnityProjectInfo"/>, get where the solution file will be exported.
        /// </summary>
        /// <param name="unityProjectInfo">This contains parsed data about the current Unity project.</param>
        /// <returns>The path to where the .sln file will be exported.</returns>
        string GetSolutionFilePath(UnityProjectInfo unityProjectInfo);

        /// <summary>
        /// Creates an exporter for the commom MSBuild file that is expected to be used by both generated (by MSBuildForUnity) and non-generated (NuGet .targets/.props, or hand-crafted) projects alike.
        /// </summary>
        /// <param name="path">The <see cref="FileInfo"/> representing where this props file will be written.</param>
        ICommonPropsExporter CreateCommonPropsExporter(FileInfo path);

        /// <summary>
        /// Creates an exporter for the top-level dependencies project that is responsible for bringing in the MSB4U resolved dependencies into the Unity project.
        /// </summary>
        /// <param name="projectPath">The path to the project output.</param>
        /// <param name="generatedProjectFolder">The path to the generated project folder.</param>
        ITopLevelDependenciesProjectExporter CreateTopLevelDependenciesProjectExporter(FileInfo projectPath, DirectoryInfo generatedProjectFolder);

        /// <summary>
        /// Creates the platform props file exporter.
        /// </summary>
        /// <param name="path">The <see cref="FileInfo"/> representing where this props file will be written.</param>
        /// <param name="unityConfiguration">The configuration for the platform props.</param>
        /// <param name="unityPlatform">The unity platform for the platform props.</param>
        /// <param name="scriptingBackend">The scripting backend for the platform props.</param>
        IPlatformPropsExporter CreatePlatformPropsExporter(FileInfo path, string unityConfiguration, string unityPlatform, ScriptingBackend scriptingBackend);

        /// <summary>
        /// Creates the specialized platform props file exporter for Player|WSA combination.
        /// </summary>
        /// <param name="path">The <see cref="FileInfo"/> representing where this props file will be written.</param>
        /// <param name="scriptingBackend">The scripting backend for the platform props.</param>
        IWSAPlayerPlatformPropsExporter CreateWSAPlayerPlatformPropsExporter(FileInfo path, ScriptingBackend scriptingBackend);

        /// <summary>
        /// Creates an exporter for a C# project.
        /// </summary>
        /// <param name="filePath">Path of th project.</param>
        /// <param name="generatedProjectFolder">The generated projects folder.</param>
        /// <param name="isGenerated">True whether this is a generated project or not.</param>
        /// <returns></returns>
        ICSharpProjectExporter CreateCSharpProjectExporter(FileInfo filePath, DirectoryInfo generatedProjectFolder, bool isGenerated);

        /// <summary>
        /// Creates a solution file exporter.
        /// </summary>
        /// <param name="logger">A logger to use for logging.</param>
        /// <param name="outputPath">The output path for the solution.</param>
        /// <returns>An instance of <see cref="ISolutionExporter"/>.</returns>
        ISolutionExporter CreateSolutionExporter(ILogger logger, FileInfo outputPath);
    }
}
#endif
