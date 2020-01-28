// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Represents a solution folder.
    /// </summary>
    public class SolutionFolder
    {
        /// <summary>
        /// Gets the name of the solution folder.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a mutable set of the children for this folder.
        /// </summary>
        public ISet<Guid> Children { get; } = new HashSet<Guid>();

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SolutionFolder(string name)
        {
            Name = name;
        }
    }
}
#endif