// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Represents a configuration/platform pair.
    /// </summary>
    public struct ConfigurationPlatformPair
    {
        /// <summary>
        /// Gets or sets the UnityConfiguration (ie. InEditor, Player)
        /// </summary>
        public UnityConfigurationType Configuration { get; set; }

        /// <summary>
        /// Gets or sets the UnityPlatform (ie. WSA, Android, IOS, etc.)
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationPlatformPair"/>.
        /// </summary>
        public ConfigurationPlatformPair(UnityConfigurationType configuration, string platform)
        {
            Configuration = configuration;
            Platform = platform;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Configuration.GetHashCode() ^ (Platform?.GetHashCode() ?? 0);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is ConfigurationPlatformPair other
                && Equals(Configuration, other.Configuration)
                && Equals(Platform, other.Platform);
        }
    }
}
#endif