// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// The specialized platform props exporter form Player|WSA pair.
    /// </summary>
    internal class TemplatedWSAPlayerPlatformPropsExporter : TemplatedPlatformPropsExporter, IWSAPlayerPlatformPropsExporter
    {
        private const string TargetUWPVersionToken = "UWP_TARGET_PLATFORM_VERSION";
        private const string MinimumUWPVersionToken = "UWP_MIN_PLATFORM_VERSION";

        public string TargetUWPVersion { get; set; }

        public string MinimumUWPVersion { get; set; }

        public TemplatedWSAPlayerPlatformPropsExporter(FileTemplate fileTemplate, FileInfo exportPath)
            : base(fileTemplate, exportPath)
        {

        }

        protected override void OnWrite(TemplatedWriter writer)
        {
            base.OnWrite(writer);

            writer.Write(TargetUWPVersionToken, TargetUWPVersion);
            writer.Write(MinimumUWPVersionToken, MinimumUWPVersion);
        }
    }
}
#endif