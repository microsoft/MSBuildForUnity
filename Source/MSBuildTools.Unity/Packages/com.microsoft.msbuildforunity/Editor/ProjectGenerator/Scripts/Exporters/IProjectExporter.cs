// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// This interface exposes teh APIs for exporting projects.
    /// </summary>
    public interface IProjectExporter
    {
        void ExportProject(UnityProjectInfo unityProjectInfo, CSProjectInfo projectInfo);

        void ExportSolution(UnityProjectInfo unityProjectInfo);
    }
}