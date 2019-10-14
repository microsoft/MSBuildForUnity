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
                if (!msBuildProjectReference.Configurations.Any())
                {
                    EditorGUILayout.HelpBox($"Define configurations below.", MessageType.Error);
                }
                else
                {
                    foreach (var configuration in msBuildProjectReference.Configurations)
                    {
                        if (GUILayout.Button(configuration.name))
                        {
                            MSBuildProjectBuilder.BuildProject(msBuildProjectReference, configuration.name);
                        }
                    }
                }
            }
        }
    }
}
