// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Represents a configuration/platform pair.
    /// </summary>
    public struct ConfigurationPlatformPair
    {
        /// <summary>
        /// Gets or sets the UnityConfiguration (ie. InEditor, Player); but may also be traditional like Debug/Release
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Gets or sets the UnityPlatform (ie. WSA, Android, IOS, etc.)
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationPlatformPair"/>.
        /// </summary>
        public ConfigurationPlatformPair(string configuration, string platform)
        {
            Configuration = configuration;
            Platform = platform;
        }

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationPlatformPair"/>.
        /// </summary>
        public ConfigurationPlatformPair(UnityConfigurationType configuration, string platform)
            : this(configuration.ToString(), platform) { }

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

        internal struct Comparer : IComparer<ConfigurationPlatformPair>
        {
            internal static Comparer Instance { get; } = new Comparer();

            public int Compare(ConfigurationPlatformPair x, ConfigurationPlatformPair y)
            {
                int results = string.Compare(x.Configuration, y.Configuration);

                return results == 0
                    ? string.Compare(x.Platform, y.Platform)
                    : results;
            }
        }
    }
}
#endif