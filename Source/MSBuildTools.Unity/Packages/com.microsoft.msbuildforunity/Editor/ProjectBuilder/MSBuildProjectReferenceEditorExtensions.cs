using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    internal static class MSBuildProjectReferenceEditorExtensions
    {
        public static void DrawBuildButtons(this MSBuildProjectReference msBuildProjectReference)
        {
            using (new EditorGUILayout.HorizontalScope())
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
        }
    }
}
