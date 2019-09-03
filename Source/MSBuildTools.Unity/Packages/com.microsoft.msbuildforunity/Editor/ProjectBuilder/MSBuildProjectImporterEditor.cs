using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    partial class MSBuildProjectImporter
    {
        [CustomEditor(typeof(MSBuildProjectImporter))]
        public sealed class MSBuildProjectImporterEditor : ScriptedImporterEditor
        {
            public override void OnInspectorGUI()
            {
                var msBuildProjectReference = (MSBuildProjectReference)this.assetTarget;

                // Build & Rebuild buttons
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Build"))
                    {
                        MSBuildProjectBuilder.BuildProject(msBuildProjectReference);
                    }

                    if (GUILayout.Button("Rebuild"))
                    {
                        MSBuildProjectBuilder.BuildProject(msBuildProjectReference);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}