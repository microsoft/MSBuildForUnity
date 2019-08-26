﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// A helper common class to reference dependencies for a CS Project.
    /// </summary>
    /// <typeparam name="T">The type of dependency.</typeparam>
    public class CSProjectDependency<T>
    {
        /// <summary>
        /// Get the actual dependency.
        /// </summary>
        public T Dependency { get; }

        /// <summary>
        /// Get a list of supported editor build targets.
        /// </summary>
        public HashSet<BuildTarget> InEditorSupportedPlatforms { get; }

        /// <summary>
        /// Get a list of supported player build targets.
        /// </summary>
        public HashSet<BuildTarget> PlayerSupportedPlatforms { get; }

        /// <summary>
        /// Creates a new dependency instance given a dependency, a set of editor supported platforms and player supported platforms.
        /// </summary>
        public CSProjectDependency(T dependency, HashSet<BuildTarget> inEditorSupportedPlatforms, HashSet<BuildTarget> playerSupportedPlatforms)
        {
            Dependency = dependency;
            InEditorSupportedPlatforms = inEditorSupportedPlatforms;
            PlayerSupportedPlatforms = playerSupportedPlatforms;
        }
    }
}
#endif