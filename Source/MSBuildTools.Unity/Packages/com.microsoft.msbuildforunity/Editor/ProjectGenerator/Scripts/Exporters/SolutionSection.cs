// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// The type of section (either pre- or post-), i.e. for project: PreProject or PostProject.
    /// </summary>
    public enum SectionType
    {
        PreSection,
        PostSection
    }

    /// <summary>
    /// This represents a solution section, either at the global level or project level.
    /// </summary>
    public class SolutionSection
    {
        /// <summary>
        /// Gets or sets the name of this solution section.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type of this solution section.
        /// </summary>
        public SectionType Type { get; set; }

        /// <summary>
        /// Gets the section lines of this generic solution section.
        /// </summary>
        public IList<string> SectionLines { get; } = new List<string>();

        /// <inherit-doc/>
        public override bool Equals(object obj)
        {
            return obj is SolutionSection other
                && Equals(Name, other.Name)
                && Type == other.Type;
        }

        /// <inherit-doc/>
        public override int GetHashCode()
        {
            return (Name?.GetHashCode() ?? 0) ^ Type.GetHashCode();
        }
    }
}
#endif