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
        /// Given a C# project, get it's export path.
        /// </summary>
        /// <param name="projectInfo">The parsed <see cref="CSProjectInfo"/> representing the C# project.</param>
        /// <returns>The path to the project.</returns>
        FileInfo GetProjectPath(CSProjectInfo projectInfo);

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
    }
}
#endif