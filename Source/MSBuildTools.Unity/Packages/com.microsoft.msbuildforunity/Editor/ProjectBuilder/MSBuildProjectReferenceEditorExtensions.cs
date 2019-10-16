using System.Linq;
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
                if (!msBuildProjectReference.Profiles.Any())
                {
                    EditorGUILayout.HelpBox($"Define profiles below.", MessageType.Error);
                }
                else
                {
                    foreach (var profile in msBuildProjectReference.Profiles)
                    {
                        if (GUILayout.Button(profile.name))
                        {
                            MSBuildProjectBuilder.BuildProject(msBuildProjectReference, profile.name);
                        }
                    }
                }
            }
        }
    }
}
