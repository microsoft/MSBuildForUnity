// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Defines a Plugin reference (WinMD, Dll)
    /// </summary>
    public struct PluginReference
    {
        /// <summary>
        /// Name of the plugin reference.
        /// </summary>
        public string ReferenceName { get; set; }

        /// <summary>
        /// Gets or set the hint path for the plugin reference.
        /// </summary>
        public Uri HintPath { get; set; }

        /// <summary>
        /// Condition for this reference.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Createsa new instance of the PluginReference.
        /// </summary>
        public PluginReference(string referenceName, Uri hintPath, string condition)
        {
            ReferenceName = referenceName;
            HintPath = hintPath;
            Condition = condition;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HintPath?.GetHashCode() ?? 0;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is PluginReference other && Equals(HintPath, other.HintPath);
        }
    }
}
#endif