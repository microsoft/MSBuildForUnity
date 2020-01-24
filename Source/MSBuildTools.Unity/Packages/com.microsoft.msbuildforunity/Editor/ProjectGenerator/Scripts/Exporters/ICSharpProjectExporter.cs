// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// The exporter for C# projects.
    /// </summary>
    public interface ICSharpProjectExporter
    {
        /// <summary>
        /// Gets or sets the project guid.
        /// </summary>
        Guid Guid { get; set; }

        /// <summary>
        /// Gets or sets the project name.
        /// </summary>
        string ProjectName { get; set; }

        /// <summary>
        /// Gets or sets whether this project is generated.
        /// </summary>
        bool IsGenerated { get; set; }

        /// <summary>
        /// Gets or sets whether this projects allow "unsafe" language features.
        /// </summary>
        bool AllowUnsafe { get; set; }

        /// <summary>
        /// Gets or sets the C# language version.
        /// </summary>
        string LanguageVersion { get; set; }

        /// <summary>
        /// Gets or sets whether this is an editor-only project (and should reference UnityEditor.dll).
        /// </summary>
        bool IsEditorOnlyProject { get; set; }

        /// <summary>
        /// Gets or sets the default platform for this project.
        /// </summary>
        string DefaultPlatform { get; set; }

        /// <summary>
        /// Gets or sets the primary source include path for this project.
        /// </summary>
        DirectoryInfo SourceIncludePath { get; set; }

        /// <summary>
        /// Gets or sets the source exclude paths, in case of folder-nested projects.
        /// </summary>
        HashSet<DirectoryInfo> SourceExcludePaths { get; }

        /// <summary>
        /// Gets or sets the platforms this project supports.
        /// </summary>
        HashSet<string> SupportedPlatforms { get; }

        /// <summary>
        /// Gets or sets the assembly search paths for this project.
        /// </summary>
        HashSet<string> AssemblySearchPaths { get; }

        /// <summary>
        /// Gets or sets the plugin references (DLLs, WinMDs)
        /// </summary>
        Dictionary<UnityConfigurationType, HashSet<PluginReference>> PluginReferences { get; }

        /// <summary>
        /// Gets or sets the projects that this project references.
        /// </summary>
        Dictionary<UnityConfigurationType, HashSet<ProjectReference>> ProjectReferences { get; }

        /// <summary>
        /// Gets or sets the platforms that this project builds for.
        /// </summary>
        HashSet<ConfigurationPlatformPair> SupportedBuildPlatforms { get; }

        /// <summary>
        /// Writes out the data to disk.
        /// </summary>
        void Write();
    }
}
#endif