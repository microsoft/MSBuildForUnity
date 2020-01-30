// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Represents a project reference with a condition.
    /// </summary>
    public struct ProjectReference
    {
        /// <summary>
        /// Path to the project.
        /// </summary>
        public Uri ReferencePath { get; set; }

        /// <summary>
        /// Condition for this reference.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Whether this is an MSBuildForUnity reference or not.
        /// </summary>
        public bool IsGenerated { get; set; }

        public ProjectReference(Uri referencePath, string condition, bool isGenerated)
        {
            ReferencePath = referencePath;
            Condition = condition;
            IsGenerated = isGenerated;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return ReferencePath?.GetHashCode() ?? 0;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is ProjectReference other && Equals(ReferencePath, other.ReferencePath);
        }
    }
}
#endif