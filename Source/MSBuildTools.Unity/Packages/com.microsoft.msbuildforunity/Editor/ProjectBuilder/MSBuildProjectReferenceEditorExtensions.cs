using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    internal static class MSBuildProjectReferenceEditorExtensions
    {
        public static async Task DrawBuildButtons(this MSBuildProjectReference msBuildProjectReference)
        {
            using (new EditorGUILayout.HorizontalScope())
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
        }
    }
}
