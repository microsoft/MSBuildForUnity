// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// An exporter for the MSBuild solution.
    /// </summary>
    public interface ISolutionExporter
    {
        /// <summary>
        /// Gets a mutable dictionary of projects to export.
        /// </summary>
        IDictionary<Guid, SolutionProject> Projects { get; }

        /// <summary>
        /// Gets a mutable dictionary of folders to export.
        /// </summary>
        IDictionary<Guid, SolutionFolder> Folders { get; }

        /// <summary>
        /// Gets a mutable dictionary of configuration/platform sets for the solution.
        /// </summary>
        ISet<ConfigurationPlatformPair> ConfigurationPlatforms { get; }
        
        /// <summary>
        /// Gets a mutable set of items that considered generated and will be exported as such.
        /// </summary>
        ISet<Guid> GeneratedItems { get; }

        /// <summary>
        /// Gets or sets the mutable dictionary of solution properties to export.
        /// </summary>
        IDictionary<string, string> Properties { get; }

        /// <summary>
        /// Gets or sets the mutable dictionary of solution notes to export.
        /// </summary>
        IDictionary<string, string> Notes { get; }

        /// <summary>
        /// Gets or sets the mutable dictionary of solution extensibility globals to export.
        /// </summary>
        IDictionary<string, string> ExtensibilityGlobals { get; }

        /// <summary>
        /// Gets or sets the mutable dictionary of additional global solution sections to export.
        /// </summary>
        IDictionary<string, SolutionSection> AdditionalSections { get; set; }

        /// <summary>
        /// Writes the contents to file.
        /// </summary>
        void Write();
    }
}
#endif