using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    partial class MSBuildProjectImporter
    {
        [CustomEditor(typeof(MSBuildProjectImporter))]
        private sealed class MSBuildProjectImporterEditor : ScriptedImporterEditor
        {
            public override void OnInspectorGUI()
            {
                var msBuildProjectReference = (MSBuildProjectReference)this.assetTarget;

                // Build & Rebuild buttons
                msBuildProjectReference.DrawBuildButtons();

                Editor.DrawPropertiesExcluding(this.serializedObject, "m_Script");

                this.ApplyRevertGUI();
            }
        }
    }
}