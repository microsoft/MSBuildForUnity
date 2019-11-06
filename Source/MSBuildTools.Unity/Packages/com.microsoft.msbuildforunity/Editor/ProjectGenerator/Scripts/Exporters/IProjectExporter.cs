// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// This interface exposes the APIs for exporting projects.
    /// </summary>
    public interface IProjectExporter
    {
        /// <summary>
        /// Given a C# project, get its export path.
        /// </summary>
        /// <param name="projectInfo">The parsed <see cref="CSProjectInfo"/> representing the C# project.</param>
        /// <returns>The path to the project.</returns>
        FileInfo GetProjectPath(CSProjectInfo projectInfo);

        /// <summary>
        /// Given the <see cref="UnityProjectInfo"/>, get where the solution file will be exported.
        /// </summary>
        /// <param name="unityProjectInfo">This contains parsed data about the current Unity project.</param>
        /// <returns>The path to where the .sln file will be exported.</returns>
        string GetSolutionFilePath(UnityProjectInfo unityProjectInfo);

        /// <summary>
        /// Exports a C# project given the <see cref="UnityProjectInfo"/> information.
        /// </summary>
        /// <param name="unityProjectInfo">This contains parsed data about the current Unity project.</param>
        /// <param name="projectInfo">The parsed <see cref="CSProjectInfo"/> representing the C# project.</param>
        void ExportProject(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo);

        /// <summary>
        /// Exports the MSBuild solution given the <see cref="UnityProjectInfo"/> information.
        /// </summary>
        /// <param name="unityProjectInfo">This contains parsed data about the current Unity project.</param>
        void ExportSolution(UnityProjectInfo unityProjectInfo);

        /// <summary>
        /// Generates the Directory.Build.props file that is expected to be used by both generated and non-generated projects alike.
        /// </summary>
        /// <param name="unityProjectInfo">This contains parsed data about the current Unity project.</param>
        void GenerateDirectoryPropsFile(UnityProjectInfo unityProjectInfo);

        /// <summary>
        /// Exports the Common props file based on the given compilation platform and whether to export it as an In-Editor flavor vs Player.
        /// </summary>
        /// <param name="platform">The platform to export.</param>
        /// <param name="inEditorConfiguration">True if this is an In-Editor flavor, false otherwise.</param>
        void ExportPlatformPropsFile(CompilationPlatformInfo platform, bool inEditorConfiguration);
    }
}
#endif
