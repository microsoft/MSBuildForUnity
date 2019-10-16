using System;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    [Serializable]
    public sealed class MSBuildBuildProfile
    {
        [SerializeField]
        [Tooltip("The name of the profile.")]
        private string name = null;

        [SerializeField]
        [Tooltip("Indicates whether the referenced MSBuild project should automatically be built.")]
        private bool autoBuild = false;

        [SerializeField]
        [Tooltip("The arguments passed to MSBuild when building the project with this profile.")]
        private string arguments = null;

        public string Name => this.name;

        public bool AutoBuild => this.autoBuild;

        public string Arguments => this.arguments;

        public static MSBuildBuildProfile Create(string name, bool autoBuild, string arguments) => new MSBuildBuildProfile { name = name, autoBuild = autoBuild, arguments = arguments };
    }
}
