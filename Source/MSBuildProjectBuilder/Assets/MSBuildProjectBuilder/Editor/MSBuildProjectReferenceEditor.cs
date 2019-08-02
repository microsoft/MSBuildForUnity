using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSBuildForUnity
{
    partial class MSBuildProjectReference
    {
        [CustomEditor(typeof(MSBuildProjectReference))]
        private sealed class MSBuildProjectReferenceEditor : Editor
        {
            private SerializedProperty autoBuildProperty;

            public override async void OnInspectorGUI()
            {
                var msBuildProjectReference = (MSBuildProjectReference)this.target;

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

                // Project path selection
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField($"Project Path: {msBuildProjectReference.ProjectPath}");

                    if (GUILayout.Button("Browse", GUILayout.MaxWidth(100)))
                    {
                        string projectPath = Path.GetDirectoryName(Application.dataPath);
                        string assetPath = Path.Combine(projectPath, AssetDatabase.GetAssetPath(msBuildProjectReference));
                        string pickedPath = EditorUtility.OpenFilePanel("Pick MSBuild Project", Application.dataPath, "*");

                        if (!string.IsNullOrEmpty(pickedPath))
                        {
                            var assetUri = new Uri(assetPath);
                            var pickedUri = new Uri(pickedPath);
                            var relativeUri = assetUri.MakeRelativeUri(pickedUri);

                            msBuildProjectReference.projectPath = relativeUri.ToString();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                // AutoBuild check box
                EditorGUILayout.PropertyField(this.autoBuildProperty);

                this.serializedObject.ApplyModifiedProperties();
            }

            private void OnEnable()
            {
                this.autoBuildProperty = this.serializedObject.FindProperty(nameof(MSBuildProjectReference.autoBuild));
            }
        }
    }
}