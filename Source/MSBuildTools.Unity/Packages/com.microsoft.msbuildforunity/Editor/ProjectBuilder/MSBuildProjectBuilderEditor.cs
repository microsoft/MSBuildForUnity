using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    partial class MSBuildProjectBuilder
    {
        private const string BuildAllProjectsMenuName = "MSBuild/Build All Projects";
        private const string BuildConfigurationName = "Build";
        private const string RebuildAllProjectsMenuName = "MSBuild/Rebuild All Projects";
        private const string RebuildConfigurationName = "Rebuild";
        private const string PackAllProjectsMenuName = "MSBuild/Pack All Projects";
        private const string PackConfigurationName = "Pack";

        /// <summary>
        /// Tries to build all projects that define the specified configuration.
        /// </summary>
        /// <param name="configuration">The name of the configuration to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        public static void TryBuildAllProjects(string configuration, string additionalArguments = "")
        {
            try
            {
                MSBuildProjectBuilder.BuildAllProjects(configuration, additionalArguments);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Canceled building MSBuild projects.");
            }
        }

        /// <summary>
        /// Determines whether the specified configuration can currently be built.
        /// </summary>
        /// <param name="configuration">The name of the configuration to build.</param>
        /// <returns>True if the specified configuration can currently be built.</returns>
        public static bool CanBuildAllProjects(string configuration)
        {
            // Only allow one build at a time (with the default UI)
            if (!MSBuildProjectBuilder.isBuildingWithDefaultUI)
            {
                // Verify at least one project defines the specified configuration.
                (IEnumerable<MSBuildProjectReference> withConfiguration, _) = MSBuildProjectBuilder.SplitByConfiguration(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences(), configuration);
                return withConfiguration.Any();
            }

            return false;
        }

        [MenuItem(MSBuildProjectBuilder.BuildAllProjectsMenuName, priority = 1)]
        private static void BuildAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.BuildConfigurationName);

        [MenuItem(MSBuildProjectBuilder.BuildAllProjectsMenuName, validate = true)]
        private static bool CanBuildAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.BuildConfigurationName);

        [MenuItem(MSBuildProjectBuilder.RebuildAllProjectsMenuName, priority = 2)]
        private static void RebuildAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.RebuildConfigurationName);

        [MenuItem(MSBuildProjectBuilder.RebuildAllProjectsMenuName, validate = true)]
        private static bool CanRebuildAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.RebuildConfigurationName);

        [MenuItem(MSBuildProjectBuilder.PackAllProjectsMenuName, priority = 3)]
        private static void PackAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.PackConfigurationName);

        [MenuItem(MSBuildProjectBuilder.PackAllProjectsMenuName, validate = true)]
        private static bool CanPackAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.PackConfigurationName);

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

            //[MenuItem("MSBuild/Auto Build All Projects [testing only]", priority = int.MaxValue)]
            private static void BuildAllAutoBuiltProjects()
            {
                (IEnumerable<MSBuildProjectReference> withConfiguration, _) = MSBuildProjectBuilder.SplitByConfiguration(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences(), "Build");
                MSBuildProjectBuilder.BuildProjects(withConfiguration.Where(projectReference => projectReference.AutoBuild).ToArray(), "Build");
            }
        }
    }
}