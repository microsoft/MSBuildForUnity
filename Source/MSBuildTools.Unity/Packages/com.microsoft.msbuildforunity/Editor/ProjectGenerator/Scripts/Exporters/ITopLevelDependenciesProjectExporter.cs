using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    /// <summary>
    /// Represents an exporter for the top-level project that is responsible for bringing in the MSB4U resovled dependencies into Unity.
    /// </summary>
    public interface ITopLevelDependenciesProjectExporter
    {
        /// <summary>
        /// Gets or sets the Guid of the project.
        /// </summary>
        Guid Guid { get; set; }

        /// <summary>
        /// Gets the set of references for this project.
        /// </summary>
        HashSet<ProjectReference> References { get; }

        void Write();
    }
}
