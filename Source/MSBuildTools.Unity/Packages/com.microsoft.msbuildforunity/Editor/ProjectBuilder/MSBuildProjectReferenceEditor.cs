using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    partial class MSBuildProjectReference
    {
        [CustomEditor(typeof(MSBuildProjectReference))]
        private sealed class MSBuildProjectReferenceEditor : Editor
        {
            private SerializedProperty projectPathProperty;

            public override void OnInspectorGUI()
            {
                var msBuildProjectReference = (MSBuildProjectReference)this.target;

                // Project path not found error
                if (!File.Exists(msBuildProjectReference.ProjectPath))
                {
                    EditorGUILayout.HelpBox($"The referenced project does not exist.", MessageType.Error);
                }
                // Build & Rebuild buttons
                else
                {
                    msBuildProjectReference.DrawBuildButtons();
                }

                // Project path selection
                using (new EditorGUILayout.HorizontalScope())
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

                            projectPathProperty.stringValue = Uri.UnescapeDataString(relativeUri.ToString());
                        }
                    }
                }

                Editor.DrawPropertiesExcluding(this.serializedObject, "m_Script", nameof(msBuildProjectReference.projectPath));

                this.serializedObject.ApplyModifiedProperties();
            }

            private void OnEnable()
            {
                this.projectPathProperty = this.serializedObject.FindProperty(nameof(MSBuildProjectReference.projectPath));
            }
        }
    }
}