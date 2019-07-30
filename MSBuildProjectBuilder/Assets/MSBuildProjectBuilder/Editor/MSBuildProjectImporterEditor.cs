﻿using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace MSBuildForUnity
{
    partial class MSBuildProjectImporter
    {
        [CustomEditor(typeof(MSBuildProjectImporter))]
        public sealed class MSBuildProjectImporterEditor : ScriptedImporterEditor
        {
            public override async void OnInspectorGUI()
            {
                var msBuildProjectReference = (MSBuildProjectReference)this.assetTarget;

                // Build & Rebuild buttons
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Build"))
                    {
                        await MSBuildProjectBuilder.BuildProjectAsync(msBuildProjectReference);
                    }

                    if (GUILayout.Button("Rebuild"))
                    {
                        await MSBuildProjectBuilder.BuildProjectAsync(msBuildProjectReference);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}