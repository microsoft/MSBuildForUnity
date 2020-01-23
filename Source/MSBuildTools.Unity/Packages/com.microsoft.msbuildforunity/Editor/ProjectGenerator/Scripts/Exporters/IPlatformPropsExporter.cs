// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Exporter for the platform props file.
    /// </summary>
    public interface IPlatformPropsExporter
    {
        /// <summary>
        /// Gets or sets the TargetFramework of the platform.
        /// </summary>
        string TargetFramework { get; set; }

        /// <summary>
        /// Gets a set of define constants for the platform.
        /// </summary>
        HashSet<string> DefineConstants { get; }

        /// <summary>
        /// Gets a set of assembly search paths for the platform.
        /// </summary>
        HashSet<string> AssemblySearchPaths { get; }
        
        /// <summary>
        /// Gets a set of references for the platform.
        /// </summary>
        Dictionary<string, Uri> References { get; }

        /// <summary>
        /// Writes out the data.
        /// </summary>
        void Write();
    }

    /// <summary>
    /// Specialized exporter for the Player|WSA platform.
    /// </summary>
    public interface IWSAPlayerPlatformPropsExporter : IPlatformPropsExporter
    {
        /// <summary>
        /// Gets or sets the target UWP version.
        /// </summary>
        string TargetUWPVersion { get; set; }

        /// <summary>
        /// Gets or sets the minimum UWP version.
        /// </summary>
        string MinimumUWPVersion { get; set; }
    }
}
#endif