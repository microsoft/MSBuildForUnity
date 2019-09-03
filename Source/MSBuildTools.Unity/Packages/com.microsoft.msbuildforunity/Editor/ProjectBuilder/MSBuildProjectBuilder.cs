using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity
{
    /// <summary>
    /// Provides logic for building MSBuild project (and solution) files.
    /// </summary>
    public static partial class MSBuildProjectBuilder
    {
        public enum ProgressMessageType
        {
            Information,
            Warning,
            Error,
        }

        public const string BuildTargetArgument = "/t:Build";
        public const string RebuildTargetArgument = "/t:Rebuild";
        public const string DefaultBuildArguments = MSBuildProjectBuilder.BuildTargetArgument;

        private static readonly Regex msBuildErrorFormat = new Regex(@"^\s*(((?<ORIGIN>(((\d+>)?[a-zA-Z]?:[^:]*)|([^:]*))):)|())(?<SUBCATEGORY>(()|([^:]*? )))(?<CATEGORY>(error|warning))( \s*(?<CODE>[^: ]*))?\s*:(?<TEXT>.*)$", RegexOptions.Compiled);

        private static bool isBuildingWithDefaultUI = false;

        /// <summary>
        /// Builds all MSBuild projects referenced by a <see cref="MSBuildProjectReference"/> within the Unity project with the default UI.
        /// </summary>
        /// <param name="arguments">The additional arguments passed to MSBuild.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static bool BuildAllProjects(string arguments = MSBuildProjectBuilder.DefaultBuildArguments)
        {
            return MSBuildProjectBuilder.BuildProjects(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences().ToArray(), arguments);
        }

        /// <summary>
        /// Builds all MSBuild projects referenced by a <see cref="MSBuildProjectReference"/> within the Unity project.
        /// </summary>
        /// <param name="arguments">The additional arguments passed to MSBuild.</param>
        /// <param name="progress">Receives progress of the build.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the build.</param>
        /// <returns></returns>
        public static Task<bool> BuildAllProjectsAsync(string arguments, IProgress<(int completedProjects, (string progressMessage, ProgressMessageType progressMessageType))> progress, CancellationToken cancellationToken)
        {
            return MSBuildProjectBuilder.BuildProjectsAsync(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences().ToArray(), arguments, progress, cancellationToken);
        }

        /// <summary>
        /// Builds the specified MSBuild projects with the default UI.
        /// </summary>
        /// <param name="msBuildProjectReferences">The collection of MSBuild projects to build.</param>
        /// <param name="arguments">The additional arguments passed to MSBuild.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static bool BuildProjects(this IReadOnlyCollection<MSBuildProjectReference> msBuildProjectReferences, string arguments = MSBuildProjectBuilder.DefaultBuildArguments)
        {
            // This method blocks so it should only be possible to have one call at a time (unless it is called from the wrong thread).
            Debug.Assert(!MSBuildProjectBuilder.isBuildingWithDefaultUI);
            MSBuildProjectBuilder.isBuildingWithDefaultUI = true;
            try
            {
                // When in batch mode, simply log the progress.
                if (Application.isBatchMode)
                {
                    return MSBuildProjectBuilder.BuildProjectsAsync(
                        msBuildProjectReferences,
                        $"{arguments} /v:diagnostic",
                        new DelegateProgress<(int, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)>(report => MSBuildProjectBuilder.LogProgressMessage(report.progressUpdate.progressMessage, report.progressUpdate.progressMessageType)),
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                // Otherwise, show a progress bar.
                else
                {
                    try
                    {
                        int completedProjects = 0;
                        string progressMessage = string.Empty;
                        var cancellationTokenSource = new CancellationTokenSource();

                        DisplayProgress();

                        Task<bool> buildTask = MSBuildProjectBuilder.BuildProjectsAsync(
                            msBuildProjectReferences,
                            $"{arguments} /v:minimal",
                            new DelegateProgress<(int completedProjects, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)>(report =>
                            {
                                if (report.progressUpdate.progressMessageType != ProgressMessageType.Information)
                                {
                                    MSBuildProjectBuilder.LogProgressMessage(report.progressUpdate.progressMessage, report.progressUpdate.progressMessageType);
                                }
                                (completedProjects, progressMessage) = (report.completedProjects, report.progressUpdate.progressMessage);
                            }),
                            cancellationTokenSource.Token);

                        // NOTE: The entire build needs to complete in a single frame, which is why this is polling.
                        // If the projects have output (such as DLLs) that are consumed by Unity, and we don't do this all in a single frame, then Unity will start trying to import assets produced by
                        // the MSBuild projects before the build is done, which will then cause all scripts (including this one) to e recompiled, which will stop the build part way through.
                        while (!buildTask.IsCompleted)
                        {
                            DisplayProgress(progressMessage, completedProjects / (float)msBuildProjectReferences.Count);
                            Thread.Sleep(33);
                        }

                        return buildTask.GetAwaiter().GetResult();

                        void DisplayProgress(string status = "", float progress = 0)
                        {
                            if (EditorUtility.DisplayCancelableProgressBar("Building MSBuild projects...", status, progress))
                            {
                                cancellationTokenSource.Cancel();
                            }
                        }
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            finally
            {
                MSBuildProjectBuilder.isBuildingWithDefaultUI = false;

                // Refresh the asset database so that it sees any new assets that were generated by building the MSBuild projects.
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Builds the specified MSBuild projects.
        /// </summary>
        /// <param name="msBuildProjectReferences">The collection of MSBuild projects to build.</param>
        /// <param name="arguments">The additional arguments passed to MSBuild.</param>
        /// <param name="progress">Receives progress of the build.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the build.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static async Task<bool> BuildProjectsAsync(this IReadOnlyCollection<MSBuildProjectReference> msBuildProjectReferences, string arguments, IProgress<(int completedProjects, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)> progress, CancellationToken cancellationToken)
        {
            bool succeeded = true;
            int completedProjects = 0;

            // NOTE: The ToArray is intentional because we need to access the ProjectPath property from the Unity UI thread, but the continuations will run on a ThreadPool thread (to enable callers to block on the returned Task if needed).
            foreach (string projectPath in msBuildProjectReferences.Select(projectReference => projectReference.ProjectPath).ToArray())
            {
                succeeded &= (await MSBuildProjectBuilder.BuildProjectAsync(
                    projectPath,
                    arguments,
                    new DelegateProgress<(string progressMessage, ProgressMessageType progressMessageType)>(report => progress.Report((completedProjects, report))),
                    cancellationToken).ConfigureAwait(false)) == 0;
                completedProjects++;
            }

            return succeeded;
        }

        /// <summary>
        /// Builds the specified MSBuild project with the default UI.
        /// </summary>
        /// <param name="msBuildProjectReference">The MSBuild project to build.</param>
        /// <param name="arguments">The additional arguments passed to MSBuild.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static bool BuildProject(this MSBuildProjectReference mSBuildProjectReference, string arguments = MSBuildProjectBuilder.DefaultBuildArguments)
        {
            return MSBuildProjectBuilder.BuildProjects(new[] { mSBuildProjectReference }, arguments);
        }

        /// <summary>
        /// Builds the specified MSBuild project.
        /// </summary>
        /// <param name="msBuildProjectReference">The MSBuild project to build.</param>
        /// <param name="arguments">The additional arguments passed to MSBuild.</param>
        /// <param name="progress">Receives progress of the build.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the build.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static Task<bool> BuildProjectAsync(this MSBuildProjectReference msBuildProjectReference, string arguments, IProgress<(string progressMessage, ProgressMessageType progressMessageType)> progress, CancellationToken cancellationToken)
        {
            return MSBuildProjectBuilder.BuildProjectsAsync(new[] { msBuildProjectReference }, arguments, new DelegateProgress<(int, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)>(report => progress.Report(report.progressUpdate)), cancellationToken);
        }

        private static IEnumerable<MSBuildProjectReference> EnumerateAllMSBuildProjectReferences()
        {
            return from assetGuid in AssetDatabase.FindAssets($"t:{nameof(MSBuildProjectReference)}")
                   let assetPath = AssetDatabase.GUIDToAssetPath(assetGuid)
                   select AssetDatabase.LoadAssetAtPath<MSBuildProjectReference>(assetPath);
        }

        private static void LogProgressMessage(string message, ProgressMessageType type)
        {
            if (type == ProgressMessageType.Error)
            {
                Debug.LogError(message);
            }
            else if (type == ProgressMessageType.Warning)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static Task<int> BuildProjectAsync(string projectPath, string arguments, IProgress<(string progressMessage, ProgressMessageType progressMessageType)> progress, CancellationToken cancellationToken)
        {
            return MSBuildProjectBuilder.RunDotNetAsync(projectPath, "msbuild", $"-restore {arguments}", progress, cancellationToken);
        }

        private static async Task<int> RunDotNetAsync(string projectPath, string command, string arguments, IProgress<(string progressMessage, ProgressMessageType progressMessageType)> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var process = new System.Diagnostics.Process { EnableRaisingEvents = true })
            {
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = $"{command} {projectPath} {arguments} -nologo ";

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.WorkingDirectory = Path.GetDirectoryName(projectPath);

                if (progress != null)
                {
                    progress.Report(($"Building {projectPath}...", ProgressMessageType.Information));

                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    process.OutputDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            var progressMessageType = ProgressMessageType.Information;
                            if (MSBuildProjectBuilder.msBuildErrorFormat.Match(e.Data) is Match match && match.Success)
                            {
                                if (match.Groups["CATEGORY"].Value == "warning")
                                {
                                    progressMessageType = ProgressMessageType.Warning;
                                }
                                else if (match.Groups["CATEGORY"].Value == "error")
                                {
                                    progressMessageType = ProgressMessageType.Error;
                                }
                            }
                            progress.Report((e.Data, progressMessageType));
                        }
                    };

                    process.ErrorDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            progress.Report((e.Data, ProgressMessageType.Error));
                        }
                    };
                }

                using (cancellationToken.Register(process.Kill))
                {
                    var taskCompletionSource = new TaskCompletionSource<int>();

                    process.Start();
                    if (progress != null)
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }

                    process.Exited += delegate
                    {
                        // When the process successfully completes it may not have finished flushing output to our async receivers.  Per docs.microsoft.com, the correct way to handle this is to call
                        // WaitForExit again with no timeout specified.
                        // https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.8#System_Diagnostics_Process_WaitForExit_System_Int32_
                        // "When standard output has been redirected to asynchronous event handlers, it is possible that output processing will not have completed when this method returns. To ensure that
                        // asynchronous event handling has been completed, call the WaitForExit() overload that takes no parameter after receiving a true from this overload."
                        process.WaitForExit();

                        taskCompletionSource.SetResult(process.ExitCode);
                    };

                    cancellationToken.ThrowIfCancellationRequested();

                    return await taskCompletionSource.Task.ConfigureAwait(false);
                }
            }
        }

        // This is similar to the built in Progress class, but this implementation is a direct pass through to the delegate.
        // In comparison, the default Progress implementation posts to the synchronization context, and therefore is always asynchronous.
        private sealed class DelegateProgress<T> : IProgress<T>
        {
            private readonly Action<T> onProgress;

            public DelegateProgress(Action<T> onProgress)
            {
                this.onProgress = onProgress ?? throw new ArgumentNullException(nameof(onProgress));
            }

            public void Report(T value) => this.onProgress(value);
        }
    }
}