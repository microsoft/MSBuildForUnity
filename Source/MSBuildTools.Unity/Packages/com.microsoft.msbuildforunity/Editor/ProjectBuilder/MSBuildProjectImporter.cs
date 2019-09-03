using System;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    [ScriptedImporter(1, new[] { "csproj", "sln" })]
    internal sealed partial class MSBuildProjectImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var msBuildProjectReference = MSBuildProjectReference.FromMSBuildProject(context.assetPath);
            context.AddObjectToAsset(Path.GetFileNameWithoutExtension(context.assetPath), msBuildProjectReference);
            context.SetMainObject(msBuildProjectReference);

            // Automatically build this project if the import is happening after the Unity project has been opened.
            // If the import is happening as part of loading the project, then the generated asset will automatically be built by MSBuildProjectBuilder.
            if (EditorAnalyticsSessionInfo.elapsedTime != 0)
            {
                try
                {
                    msBuildProjectReference.BuildProject();
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning($"Canceled building {msBuildProjectReference.ProjectPath}.");
                }
            }
        }
    }
}