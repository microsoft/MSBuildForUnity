// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Represents a Solution to Project configuration/platform mappings.
    /// </summary>
    public class ProjectConfigurationPlatformMapping
    {
        /// <summary>
        /// Gets or sets the project configuration/platform target.
        /// </summary>
        public ConfigurationPlatformPair ConfigurationPlatform { get; set; }

        /// <summary>
        /// Gets or sets whether this configuration will be built under this mapping.
        /// </summary>
        public bool EnabledForBuild { get; set; }

        /// <summary>
        /// Gets a mutable dictionary of additional property mappings.
        /// </summary>
        public IDictionary<string, ConfigurationPlatformPair> AdditionalPropertyMappings { get; set; }
    }
}
#endif