// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    public static class Extensions
    {
        public static void AddProject(this ISolutionExporter exporter, SolutionProject project, bool isGenerated = false)
        {
            exporter.Projects.Add(project.Guid, project);

            if (isGenerated)
            {
                exporter.GeneratedItems.Add(project.Guid);
            }
        }

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