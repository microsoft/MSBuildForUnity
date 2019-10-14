using System;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    [Serializable]
    public sealed class MSBuildBuildConfiguration
    {
        [SerializeField]
        private string name = null;

        [SerializeField]
        private string arguments = null;

        public string Name => this.name;

        public string Arguments => this.arguments;

        public static MSBuildBuildConfiguration Create(string name, string arguments) => new MSBuildBuildConfiguration { name = name, arguments = arguments };
    }
}
