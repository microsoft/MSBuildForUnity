﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    public enum BuildEngine
    {
        DotNet,
        VisualStudio,
    }

    [CreateAssetMenu(fileName = nameof(MSBuildProjectReference), menuName = "MSBuild/Project Reference", order = 1)]
    public sealed partial class MSBuildProjectReference : ScriptableObject
    {
        private string assetRelativePath;

        [SerializeField]
        [Tooltip("The path to the MSBuild project (or solution). The path can be absolute, or relative to this asset file.")]
        private string projectPath = null;

        [SerializeField]
        [Tooltip("The MSBuild build engine to use to build the project.")]
        private BuildEngine buildEngine = BuildEngine.DotNet;

        [SerializeField]
        [Tooltip("Indicates whether the referenced MSBuild project should automatically be built.")]
        private bool autoBuild = true;

        [SerializeField]
        [Tooltip("Named argument sets to configure different build options.")]
        private MSBuildBuildConfiguration[] configurations = null;

        /// <summary>
        /// Creates an in-memory instance that can resolve the full path to the MSBuild project.
        /// </summary>
        /// <param name="assetRelativePath">The path to the <see cref="MSBuildProjectReference"/> asset.</param>
        /// <param name="autoBuild">True to enable auto build of the referenced project.</param>
        /// <returns>An <see cref="MSBuildProjectReference"/> instance.</returns>
        /// <remarks>
        /// This is useful for creating and passing transient <see cref="MSBuildProjectReference"/> instances to <see cref="MSBuildProjectBuilder"/> when they don't exist in the <see cref="AssetDatabase"/>.
        /// </remarks>
        public static MSBuildProjectReference FromMSBuildProject(string assetRelativePath, BuildEngine buildEngine = BuildEngine.DotNet, bool autoBuild = true, IEnumerable<MSBuildBuildConfiguration> configurations = null)
        {
            var msBuildProjectReference = ScriptableObject.CreateInstance<MSBuildProjectReference>();
            msBuildProjectReference.assetRelativePath = assetRelativePath;
            msBuildProjectReference.projectPath = Path.GetFileName(assetRelativePath);
            msBuildProjectReference.buildEngine = buildEngine;
            msBuildProjectReference.autoBuild = autoBuild;
            msBuildProjectReference.configurations = configurations.ToArray();
            return msBuildProjectReference;
        }

        public string ProjectPath
        {
            get
            {
                // Prefer the asset relative path if this instance was created from code.
                string assetRelativePath = this.assetRelativePath;
                if (string.IsNullOrEmpty(assetRelativePath))
                {
                    // Otherwise assume it is in the asset database and look it up.
                    assetRelativePath = AssetDatabase.GetAssetPath(this);
                }

                // Determine the absolute path of the MSBuild project (based on the projectPath being relative to the MSBuildProjectReference asset).
                if (!string.IsNullOrEmpty(this.projectPath))
                {
                    string unityProjectPath = Path.GetDirectoryName(Application.dataPath);
                    string assetAbsolutePath = Path.Combine(unityProjectPath, assetRelativePath);
                    string assetAbsoluteDirectory = Path.GetDirectoryName(assetAbsolutePath);
                    return Path.GetFullPath(Path.Combine(assetAbsoluteDirectory, this.projectPath));
                }

                return string.Empty;
            }
        }

        public BuildEngine BuildEngine => this.buildEngine;

        public bool AutoBuild => this.autoBuild;

        public IEnumerable<(string name, string arguments)> Configurations => this.configurations == null ? Enumerable.Empty<(string, string)>() : this.configurations.Select(configuration => (configuration.Name, configuration.Arguments));
    }
}