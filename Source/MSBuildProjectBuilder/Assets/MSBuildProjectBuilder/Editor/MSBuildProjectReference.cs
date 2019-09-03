using System.IO;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    [CreateAssetMenu(fileName = nameof(MSBuildProjectReference), menuName = "MSBuild/Project Reference", order = 1)]
    public sealed partial class MSBuildProjectReference : ScriptableObject
    {
        public enum WindowsBuildEngine
        {
            None,
            DotNet,
            VisualStudio2017,
            VisualStudio2019,
        }

        public enum MacBuildEngine
        {
            None,
            DotNet,
            VisualStudioForMac,
        }

        private string assetRelativePath;

        [SerializeField]
        [Tooltip("The path to the MSBuild project (or solution). The path can be absolute, or relative to this asset file.")]
        private string projectPath = null;

        [SerializeField]
        [Tooltip("The MSBuild build engine to use on Windows.")]
        private WindowsBuildEngine windowsBuildEngine = WindowsBuildEngine.DotNet;

        [SerializeField]
        [Tooltip("The MSBuild build engine to use on Mac.")]
        private MacBuildEngine macBuildEngine = MacBuildEngine.DotNet;

        [SerializeField]
        [Tooltip("Indicates whether the referenced MSBuild project should automatically be built.")]
        private bool autoBuild = true;

        /// <summary>
        /// Creates an in-memory instance that can resolve the full path to the MSBuild project.
        /// </summary>
        /// <param name="assetRelativePath">The path to the <see cref="MSBuildProjectReference"/> asset.</param>
        /// <param name="autoBuild">True to enable auto build of the referenced project.</param>
        /// <returns>An <see cref="MSBuildProjectReference"/> instance.</returns>
        /// <remarks>
        /// This is useful for creating and passing transient <see cref="MSBuildProjectReference"/> instances to <see cref="MSBuildProjectBuilder"/> when they don't exist in the <see cref="AssetDatabase"/>.
        /// </remarks>
        public static MSBuildProjectReference FromMSBuildProject(string assetRelativePath, bool autoBuild = true)
        {
            var msBuildProjectReference = ScriptableObject.CreateInstance<MSBuildProjectReference>();
            msBuildProjectReference.assetRelativePath = assetRelativePath;
            msBuildProjectReference.projectPath = Path.GetFileName(assetRelativePath);
            msBuildProjectReference.autoBuild = autoBuild;
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

        public bool AutoBuild
        {
            get => this.autoBuild;
        }
    }
}