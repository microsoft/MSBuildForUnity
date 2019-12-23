using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    partial class MSBuildProjectBuilder
    {
        public const string BuildProfileName = "Build";
        public const string RebuildProfileName = "Rebuild";
        public const string CleanProfileName = "Clean";
        public const string PackProfileName = "Pack";

        private const string BuildAllProjectsMenuName = "MSBuild/Build All Projects";
        private const string RebuildAllProjectsMenuName = "MSBuild/Rebuild All Projects";
        private const string CleanAllProjectsMenuName = "MSBuild/Clean All Projects";
        private const string PackAllProjectsMenuName = "MSBuild/Pack All Projects";

        /// <summary>
        /// Tries to build all projects that define the specified profile.
        /// </summary>
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        public static bool TryBuildAllProjects(string profile, string additionalArguments = "")
        {
            try
            {
                return MSBuildProjectBuilder.BuildAllProjects(profile, additionalArguments);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Canceled building MSBuild projects.");
                return false;
            }
        }

        /// <summary>
        /// Determines whether the specified profile can currently be built.
        /// </summary>
        /// <param name="profile">The name of the profile to build.</param>
        /// <returns>True if the specified profile can currently be built.</returns>
        public static bool CanBuildAllProjects(string profile)
        {
            // Only allow one build at a time (with the default UI)
            if (!MSBuildProjectBuilder.isBuildingWithDefaultUI)
            {
                // Verify at least one project defines the specified profile.
                (IEnumerable<MSBuildProjectReference> withProfile, _) = MSBuildProjectBuilder.SplitByProfile(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences(), profile);
                return withProfile.Any();
            }

            return false;
        }

        [MenuItem(MSBuildProjectBuilder.BuildAllProjectsMenuName, priority = 1)]
        private static void BuildAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.BuildProfileName);

        [MenuItem(MSBuildProjectBuilder.BuildAllProjectsMenuName, validate = true)]
        private static bool CanBuildAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.BuildProfileName);

        [MenuItem(MSBuildProjectBuilder.RebuildAllProjectsMenuName, priority = 2)]
        private static void RebuildAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.RebuildProfileName);

        [MenuItem(MSBuildProjectBuilder.RebuildAllProjectsMenuName, validate = true)]
        private static bool CanRebuildAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.RebuildProfileName);

        [MenuItem(MSBuildProjectBuilder.CleanAllProjectsMenuName, priority = 3)]
        private static void CleanAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.CleanProfileName);

        [MenuItem(MSBuildProjectBuilder.CleanAllProjectsMenuName, validate = true)]
        private static bool CanCleanAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.CleanProfileName);

        [MenuItem(MSBuildProjectBuilder.PackAllProjectsMenuName, priority = 4)]
        private static void PackAllProjects() => MSBuildProjectBuilder.TryBuildAllProjects(MSBuildProjectBuilder.PackProfileName);

        [MenuItem(MSBuildProjectBuilder.PackAllProjectsMenuName, validate = true)]
        private static bool CanPackAllProjects() => MSBuildProjectBuilder.CanBuildAllProjects(MSBuildProjectBuilder.PackProfileName);

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
                IEnumerable<IGrouping<string, MSBuildProjectReference>> autoBuildProfiles =
                    from projectReference in MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences()
                    from profile in projectReference.Profiles
                    where profile.autoBuild
                    group projectReference by profile.name;

                foreach (IGrouping<string, MSBuildProjectReference> autoBuildProfile in autoBuildProfiles)
                {
                    MSBuildProjectBuilder.BuildProjects(autoBuildProfile.ToArray(), autoBuildProfile.Key);
                }
            }
        }
    }
}
