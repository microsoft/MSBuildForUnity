using System;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    [Serializable]
    public sealed class MSBuildBuildProfile
    {
        [SerializeField]
        private string name = null;

        [SerializeField]
        private string arguments = null;

        public string Name => this.name;

        public string Arguments => this.arguments;

        public static MSBuildBuildProfile Create(string name, string arguments) => new MSBuildBuildProfile { name = name, arguments = arguments };
    }
}
