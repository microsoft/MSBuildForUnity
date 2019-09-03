using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    partial class MSBuildProjectBuilder
    {
        private const string BuildAllProjectsMenuName = "MSBuild/Build All Projects";
        private const string RebuildAllProjectsMenuName = "MSBuild/Rebuild All Projects";

        [MenuItem(MSBuildProjectBuilder.BuildAllProjectsMenuName)]
        private static void BuildAllProjects()
        {
            try
            {
                MSBuildProjectBuilder.BuildAllProjects(MSBuildProjectBuilder.BuildTargetArgument);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Canceled building MSBuild projects.");
            }
        }

        [MenuItem(MSBuildProjectBuilder.RebuildAllProjectsMenuName)]
        private static void RebuildAllProjects()
        {
            try
            {
                MSBuildProjectBuilder.BuildAllProjects(MSBuildProjectBuilder.RebuildTargetArgument);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Canceled building MSBuild projects.");
            }
        }

        [MenuItem(MSBuildProjectBuilder.BuildAllProjectsMenuName, validate = true)]
        [MenuItem(MSBuildProjectBuilder.RebuildAllProjectsMenuName, validate = true)]
        private static bool ValidateBuildAllProjects()
        {
            // Only allow one build at a time (with the default UI).
            return !MSBuildProjectBuilder.isBuildingWithDefaultUI;
        }

        [InitializeOnLoad]
        private sealed class BuildOnLoad
        {
            static BuildOnLoad()
            {
                // Only do this when Unity is first loading the project (not when the AppDomain reloads due to switching between play/edit mode, recompiling project scripts, etc.).
                if (EditorAnalyticsSessionInfo.elapsedTime == 0)
                {
                    // The Unity asset database cannot be queried until the Editor is fully loaded. The first editor update tick seems to be a safe bet for this. Ideally the build would happen sooner.
                    EditorApplication.update += OnUpdate;
                    void OnUpdate()
                    {
                        EditorApplication.update -= OnUpdate;
                        BuildOnLoad.BuildAllAutoBuiltProjects();
                    }
                }
            }

            [MenuItem("MSBuild/Auto Build All Projects [testing only]")]
            private static void BuildAllAutoBuiltProjects()
            {
                MSBuildProjectBuilder.BuildProjects(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences().Where(projectReference => projectReference.AutoBuild).ToArray());
            }
        }
    }
}