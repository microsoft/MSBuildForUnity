using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    [ScriptedImporter(1, new[] { "csproj", "sln" })]
    internal sealed partial class MSBuildProjectImporter : ScriptedImporter
    {
        [SerializeField]
        [Tooltip("The MSBuild build engine to use to build the project.")]
        private BuildEngine buildEngine = BuildEngine.DotNet;

        [SerializeField]
        [Tooltip("Named profiles to configure different build options.")]
        private MSBuildBuildProfile[] profiles = null;

        public override void OnImportAsset(AssetImportContext context)
        {
            var msBuildProjectReference = MSBuildProjectReference.FromMSBuildProject(context.assetPath, this.buildEngine, true, this.profiles);

            context.AddObjectToAsset(Path.GetFileNameWithoutExtension(context.assetPath), msBuildProjectReference);
            context.SetMainObject(msBuildProjectReference);

            // Automatically build this project if the import is happening after the Unity project has been opened.
            // If the import is happening as part of loading the project, then the generated asset will automatically be built by MSBuildProjectBuilder.
            if (EditorAnalyticsSessionInfo.elapsedTime != 0 && msBuildProjectReference.Profiles != null)
            {
                foreach (var profile in msBuildProjectReference.Profiles.Where(profile => profile.autoBuild))
                {
                    try
                    {
                        msBuildProjectReference.BuildProject(profile.name);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning($"Canceled building {msBuildProjectReference.ProjectPath}.");
                    }
                }
            }
        }
    }
}