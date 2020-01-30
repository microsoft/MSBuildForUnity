using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{

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
        /// Gets or sets the MSBuildForUnity version.
        /// </summary>
        Version MSBuildForUnityVersion { get; set; }

        /// <summary>
        /// Gets the set of references for this project.
        /// </summary>
        HashSet<ProjectReference> References { get; }

        void Write();
    }
}
