using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        private const string AdoAuthenticationUrl = "https://microsoft.com/devicelogin";

        private static readonly Lazy<Task<string>> msBuildPathTask = new Lazy<Task<string>>(GetMSBuildPath);
        private static readonly Lazy<string> dotnetPath = new Lazy<string>(GetDotNetPath);
        private static readonly Regex msBuildErrorFormat = new Regex(@"^\s*(((?<ORIGIN>(((\d+>)?[a-zA-Z]?:[^:]*)|([^:]*))):)|())(?<SUBCATEGORY>(()|([^:]*? )))(?<CATEGORY>(error|warning))( \s*(?<CODE>[^: ]*))?\s*:(?<TEXT>.*)$", RegexOptions.Compiled);
        private static readonly Regex adoAuthenticationFormat = new Regex($"\\s*(\\[CredentialProvider\\])?(?<Message>.*({MSBuildProjectBuilder.AdoAuthenticationUrl}).*(?<Code>[A-Z|0-9]{{9}}).*)", RegexOptions.Compiled);

        private static bool isBuildingWithDefaultUI = false;

        private static string GetDotNetPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (TryGetPathFor("dotnet.exe", out string result) ? result : "dotnet")
                : (TryGetPathFor("dotnet", out result) ? result : "/usr/local/share/dotnet/dotnet");
        }

        private static async Task<string> GetMSBuildPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? (TryGetPathFor("msbuild.exe", out string result) ? result : await GetVSWhereMSBuildPath().ConfigureAwait(false))
                : (TryGetPathFor("msbuild", out result) ? result : "/Library/Frameworks/Mono.framework/Commands/msbuild");
        }

        private static bool TryGetPathFor(string file, out string result)
        {
            string[] paths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Environment.GetEnvironmentVariable("path").Split(';')
                : Environment.GetEnvironmentVariable("PATH").Split(':');

            foreach (string path in paths)
            {
                string candidate = Path.Combine(path, file);
                if (File.Exists(candidate))
                {
                    result = candidate;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static async Task<string> GetVSWhereMSBuildPath()
        {
            string vswherePath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (!File.Exists(vswherePath))
            {
                throw new FileNotFoundException("Visual Studio Installer not found.", vswherePath);
            }

            string vswhereArguments = $"-latest -requires Microsoft.Component.MSBuild -find {Path.Combine("MSBuild", "**", "Bin", "MSBuild.exe")}";

            using (var process = new System.Diagnostics.Process { EnableRaisingEvents = true })
            {
                process.StartInfo.FileName = vswherePath;
                process.StartInfo.Arguments = vswhereArguments;

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                bool succeeded = false;
                string result = null;
                var taskCompletionSource = new TaskCompletionSource<string>();

                process.OutputDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        succeeded = true;
                        result = e.Data;
                    }
                };

                process.ErrorDataReceived += (object sender, System.Diagnostics.DataReceivedEventArgs e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        succeeded = false;
                        result = e.Data;
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.Exited += delegate
                {
                    process.WaitForExit();

                    if (succeeded)
                    {
                        taskCompletionSource.SetResult(result);
                    }
                    else
                    {
                        string message = "Could not find Visual Studio MSBuild engine.";
                        if (!string.IsNullOrEmpty(result))
                        {
                            message = $"{message} ({result})";
                        }
                        taskCompletionSource.SetException(new Exception(message));
                    }
                };

                return await taskCompletionSource.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Builds all MSBuild projects referenced by a <see cref="MSBuildProjectReference"/> within the Unity project with the default UI.
        /// </summary>
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static bool BuildAllProjects(string profile, string additionalArguments = "")
        {
            (IEnumerable<MSBuildProjectReference> withProfile, IEnumerable<MSBuildProjectReference> withoutProfile) = MSBuildProjectBuilder.SplitByProfile(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences(), profile);

            foreach (MSBuildProjectReference msBuildProjectReference in withoutProfile)
            {
                Debug.Log($"Skipping {msBuildProjectReference.ProjectPath} because it does not have a profile named {profile}.", msBuildProjectReference);
            }

            return MSBuildProjectBuilder.BuildProjects(withProfile.ToArray(), profile, additionalArguments);
        }

        /// <summary>
        /// Builds all MSBuild projects referenced by a <see cref="MSBuildProjectReference"/> within the Unity project.
        /// </summary>
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        /// <param name="progress">Receives progress of the build.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the build.</param>
        /// <returns></returns>
        public static Task<bool> BuildAllProjectsAsync(string profile, string additionalArguments, IProgress<(int completedProjects, (string progressMessage, ProgressMessageType progressMessageType))> progress, CancellationToken cancellationToken)
        {
            return MSBuildProjectBuilder.BuildProjectsAsync(MSBuildProjectBuilder.EnumerateAllMSBuildProjectReferences().ToArray(), profile, additionalArguments, progress, cancellationToken);
        }

        /// <summary>
        /// Builds the specified MSBuild projects with the default UI.
        /// </summary>
        /// <param name="msBuildProjectReferences">The collection of MSBuild projects to build.</param>
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static bool BuildProjects(this IReadOnlyCollection<MSBuildProjectReference> msBuildProjectReferences, string profile, string additionalArguments = "")
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
                        profile,
                        $" -v:diagnostic {additionalArguments}",
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
                        Match adoAuthenticationMatch = null;
                        var cancellationTokenSource = new CancellationTokenSource();

                        DisplayProgress();

                        Task<bool> buildTask = MSBuildProjectBuilder.BuildProjectsAsync(
                            msBuildProjectReferences,
                            profile,
                            $" -v:minimal -p:NuGetInteractive=true {additionalArguments}",
                            new DelegateProgress<(int completedProjects, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)>(report =>
                            {
                                if (report.progressUpdate.progressMessageType != ProgressMessageType.Information)
                                {
                                    MSBuildProjectBuilder.LogProgressMessage(report.progressUpdate.progressMessage, report.progressUpdate.progressMessageType);
                                }

                                (completedProjects, progressMessage) = (report.completedProjects, report.progressUpdate.progressMessage);

                                // Check whether the build is blocked on Azure DevOps package feed authentication (but be careful not to try to match if there is a pending successful match that has not been observed by DisplayProgress)
                                if (!(adoAuthenticationMatch?.Success == true))
                                {
                                    adoAuthenticationMatch = MSBuildProjectBuilder.adoAuthenticationFormat.Match(progressMessage);
                                }
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

                            if (adoAuthenticationMatch?.Success == true)
                            {
                                string message = $"{adoAuthenticationMatch.Groups["Message"].Value}{Environment.NewLine}{Environment.NewLine}Click OK to copy the authentication code to the clipboard and open {MSBuildProjectBuilder.AdoAuthenticationUrl} in your default browser, or click Cancel to abort the build.";
                                string azureAuthenticationCode = adoAuthenticationMatch.Groups["Code"].Value;

                                // Clear the match before asking the user the authenticate, since once this completes the build will continue and a new match can be made for an auth request on another feed.
                                adoAuthenticationMatch = null;

                                if (EditorUtility.DisplayDialog("Azure DevOps Package Feed Authentication Required", message, "OK", "Cancel"))
                                {
                                    // Copy the authentication code to the clipboard for convenience
                                    EditorGUIUtility.systemCopyBuffer = azureAuthenticationCode;

                                    // Launch the Azure DevOps device login web page
                                    Application.OpenURL(MSBuildProjectBuilder.AdoAuthenticationUrl);
                                }
                                else
                                {
                                    cancellationTokenSource.Cancel();
                                }
                            }
                        }
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
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
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        /// <param name="progress">Receives progress of the build.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the build.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static async Task<bool> BuildProjectsAsync(this IReadOnlyCollection<MSBuildProjectReference> msBuildProjectReferences, string profile, string additionalArguments, IProgress<(int completedProjects, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)> progress, CancellationToken cancellationToken)
        {
            bool succeeded = true;
            int completedProjects = 0;

            (IEnumerable<MSBuildProjectReference> withProfile, IEnumerable<MSBuildProjectReference> withoutProfile) = MSBuildProjectBuilder.SplitByProfile(msBuildProjectReferences, profile);

            foreach (MSBuildProjectReference msBuildProjectReference in withoutProfile)
            {
                Debug.LogWarning($"A profile named '{profile}' is not defined for the specified {typeof(MSBuildProjectReference).Name}.", msBuildProjectReference);
                completedProjects++;
            }

            var validConfiguredProjects = new List<(string projectPath, string arguments, BuildEngine buildEngine)>();
            foreach (MSBuildProjectReference msBuildProjectReference in withProfile)
            {
                var profiles = msBuildProjectReference.Profiles.Where(config => string.Equals(config.name, profile, StringComparison.CurrentCultureIgnoreCase)).ToArray();
                if (profiles.Length > 1)
                {
                    Debug.LogError($"Multiple profiles named '{profile}' are defined for the specified {typeof(MSBuildProjectReference).Name}.", msBuildProjectReference);
                    completedProjects++;
                }
                else
                {
                    validConfiguredProjects.Add((msBuildProjectReference.ProjectPath, profiles[0].arguments, msBuildProjectReference.BuildEngine));
                }
            }

            foreach (var projectInfo in validConfiguredProjects)
            {
                succeeded &= (await MSBuildProjectBuilder.BuildProjectAsync(
                    projectInfo.projectPath,
                    projectInfo.buildEngine,
                    $"{additionalArguments} {projectInfo.arguments}",
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
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static bool BuildProject(this MSBuildProjectReference mSBuildProjectReference, string profile, string additionalArguments = "")
        {
            return MSBuildProjectBuilder.BuildProjects(new[] { mSBuildProjectReference }, profile, additionalArguments);
        }

        /// <summary>
        /// Builds the specified MSBuild project.
        /// </summary>
        /// <param name="msBuildProjectReference">The MSBuild project to build.</param>
        /// <param name="profile">The name of the profile to build.</param>
        /// <param name="additionalArguments">The additional arguments passed to MSBuild.</param>
        /// <param name="progress">Receives progress of the build.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the build.</param>
        /// <returns>A task that will have a result of true if the build succeeds.</returns>
        public static Task<bool> BuildProjectAsync(this MSBuildProjectReference msBuildProjectReference, string profile, string additionalArguments, IProgress<(string progressMessage, ProgressMessageType progressMessageType)> progress, CancellationToken cancellationToken)
        {
            return MSBuildProjectBuilder.BuildProjectsAsync(new[] { msBuildProjectReference }, profile, additionalArguments, new DelegateProgress<(int, (string progressMessage, ProgressMessageType progressMessageType) progressUpdate)>(report => progress.Report(report.progressUpdate)), cancellationToken);
        }

        private static IEnumerable<MSBuildProjectReference> EnumerateAllMSBuildProjectReferences()
        {
            return from assetGuid in AssetDatabase.FindAssets($"t:{nameof(MSBuildProjectReference)}")
                   let assetPath = AssetDatabase.GUIDToAssetPath(assetGuid)
                   select AssetDatabase.LoadAssetAtPath<MSBuildProjectReference>(assetPath);
        }

        private static (IEnumerable<MSBuildProjectReference> withProfile, IEnumerable<MSBuildProjectReference> withoutProfile) SplitByProfile(IEnumerable<MSBuildProjectReference> msBuildProjectReferences, string profile)
        {
            var groupedProjectReferences = msBuildProjectReferences.GroupBy(msBuildProjectReference => msBuildProjectReference.Profiles?.Any(config => string.Equals(config.name, profile, StringComparison.CurrentCultureIgnoreCase)) == true);
            return
            (
                withProfile: groupedProjectReferences.SingleOrDefault(grouping => grouping.Key == true) ?? Enumerable.Empty<MSBuildProjectReference>(),
                withoutProfile: groupedProjectReferences.SingleOrDefault(grouping => grouping.Key == false) ?? Enumerable.Empty<MSBuildProjectReference>()
            );
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

        private static async Task<int> BuildProjectAsync(string projectPath, BuildEngine buildEngine, string arguments, IProgress<(string progressMessage, ProgressMessageType progressMessageType)> progress, CancellationToken cancellationToken)
        {
            arguments = $"{Path.GetFileName(projectPath)} -restore {arguments}";
            string msBuildPath = null;

            switch (buildEngine)
            {
                case BuildEngine.DotNet:
                    msBuildPath = dotnetPath.Value;

                    arguments = $"msbuild {arguments}";
                    break;

                case BuildEngine.VisualStudio:
                    msBuildPath = await MSBuildProjectBuilder.msBuildPathTask.Value.ConfigureAwait(false);
                    break;

                default:
                    throw new NotImplementedException($"{buildEngine} was specified for {projectPath} but support for {buildEngine} has not been implemented.");
            }

            if (progress != null)
            {
                progress.Report(($"Building {projectPath}...", ProgressMessageType.Information));
            }

            return await MSBuildProjectBuilder.ExecuteMSBuildAsync(msBuildPath, Path.GetDirectoryName(projectPath), arguments, progress, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> ExecuteMSBuildAsync(string msBuildPath, string workingDirectory, string arguments, IProgress<(string progressMessage, ProgressMessageType progressMessageType)> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Debug.Log($"{workingDirectory}> {msBuildPath} {arguments}");

            using (var process = new System.Diagnostics.Process { EnableRaisingEvents = true })
            {
                process.StartInfo.FileName = msBuildPath;
                process.StartInfo.Arguments = $"{arguments} -nologo";

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.WorkingDirectory = workingDirectory;

                if (progress != null)
                {
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
