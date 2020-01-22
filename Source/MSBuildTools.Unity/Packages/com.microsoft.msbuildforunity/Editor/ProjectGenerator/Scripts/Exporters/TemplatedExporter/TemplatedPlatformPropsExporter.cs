// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    /// <summary>
    /// A class for exporting platform props using templates.
    /// </summary>
    internal class TemplatedPlatformPropsExporter : IPlatformPropsExporter
    {
        private const string TargetFrameworkToken = "TARGET_FRAMEWORK";
        private const string DefineConstantsToken = "PLATFORM_COMMON_DEFINE_CONSTANTS";
        private const string AssemblySearchPathsToken = "PLATFORM_COMMON_ASSEMBLY_SEARCH_PATHS";

        private const string CommonReferenceSubTemplate = "PLATFORM_COMMON_REFERENCE";
        private const string CommonReferencesSubTemplateReferenceToken = "REFERENCE";
        private const string CommonReferencesSubTemplateHintPathToken = "HINT_PATH";

        private readonly FileTemplate fileTemplate;
        private readonly FileInfo exportPath;

        public string TargetFramework { get; set; }

        public HashSet<string> DefineConstants { get; } = new HashSet<string>(); // Guess at default size

        public HashSet<string> AssemblySearchPaths { get; } = new HashSet<string>(); // Guess at default size

        public Dictionary<string, Uri> References { get; } = new Dictionary<string, Uri>(250); // Guess at default size

        public TemplatedPlatformPropsExporter(FileTemplate fileTemplate, FileInfo exportPath)
        {
            this.fileTemplate = fileTemplate;
            this.exportPath = exportPath;
        }

        public void Write()
        {
            TemplatedWriter writer = new TemplatedWriter(fileTemplate);

            writer.Write(TargetFrameworkToken, TargetFramework, optional: true);

            writer.Write(DefineConstantsToken, DefineConstants);
            writer.Write(AssemblySearchPathsToken, AssemblySearchPaths);

            foreach (KeyValuePair<string, Uri> reference in References)
            {
                TemplatedWriter subTemplateWriter = writer.CreateWriterFor(CommonReferenceSubTemplate);
                subTemplateWriter.Write(CommonReferencesSubTemplateReferenceToken, reference.Key);
                subTemplateWriter.Write(CommonReferencesSubTemplateHintPathToken, reference.Value.LocalPath);
            }

            OnWrite(writer);

            writer.Export(exportPath);
        }

        protected virtual void OnWrite(TemplatedWriter writer) { }
    }
}
#endif