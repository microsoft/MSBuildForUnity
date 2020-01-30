// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Helper extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Adds a <see cref="SolutionProject"/> to the solution exporter, and optionally registers it as generated.
        /// </summary>
        /// <param name="exporter">The @this exporter.</param>
        /// <param name="project">The project to add.</param>
        /// <param name="isGenerated">Whether the project is generated.</param>
        public static void AddProject(this ISolutionExporter exporter, SolutionProject project, bool isGenerated = false)
        {
            exporter.Projects.Add(project.Guid, project);

            if (isGenerated)
            {
                exporter.GeneratedItems.Add(project.Guid);
            }
        }

        /// <summary>
        /// Gets or adds a <see cref="SolutionFolder"/> to the solution exporter, and optionally registers it as generated.
        /// </summary>
        /// <param name="exporter">The @this exporter.</param>
        /// <param name="folderGuid">The folder identifier.</param>
        /// <param name="folderName">The folder name.</param>
        /// <param name="isGenerated">Whether the folder is generated.</param>
        /// <returns>The fetched or created solution folder.</returns>
        public static SolutionFolder GetOrAddFolder(this ISolutionExporter exporter, Guid folderGuid, string folderName, bool isGenerated = false)
        {
            SolutionFolder toReturn = exporter.Folders.GetOrAdd(folderGuid, k => new SolutionFolder(folderName));

            if (isGenerated)
            {
                exporter.GeneratedItems.Add(folderGuid);
            }

            return toReturn;
        }
    }
}
#endif