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
    internal class TemplatedCSharpProjectExporter : ICSharpProjectExporter
    {
        private const string ProjectGuidToken = "PROJECT_GUID";
        private const string ProjectNameToken = "PROJECT_NAME";
        private const string AllowUnsafeToken = "ALLOW_UNSAFE";
        private const string LanguageVersionToken = "LANGUAGE_VERSION";
        private const string IsEditorOnlyProjectToken = "IS_EDITOR_ONLY_TARGET";
        private const string DefaultPlatformToken = "DEFAULT_PLATFORM";
        private const string SupportedPlatformsToken = "SUPPORTED_PLATFORMS";
        private const string AssemblySearchPathsToken = "ASSEMBLY_SEARCH_PATHS";

        private const string SourceIncludePathToken = "PROJECT_DIRECTORY_PATH";

        private const string SourceExcludeTemplate = "SOURCE_EXCLUDE";
        private const string SourceExcludeTemplate_ExcludePathToken = "EXCLUDE_DIRECTORY_PATH";

        private const string ProjectReferenceTemplateSet = "PROJECT_REFERENCE_SET";
        private const string PluginReferenceTemplateSet = "PLUGIN_REFERENCE_SET";
        private const string ReferenceTemplateSet_ConfigurationToken = "REFERENCE_CONFIGURATION";

        private const string ProjectReferenceTemplate = "PROJECT_REFERENCE";
        private const string ProjectReferenceTemplate_ReferenceToken = "REFERENCE";
        private const string ProjectReferenceTemplate_ConditionToken = "CONDITION";

        private const string PluginReferenceTemplate = "PLUGIN_REFERENCE";
        private const string PluginReferenceTemplate_ReferenceToken = "REFERENCE";
        private const string PluginReferenceTemplate_ConditionToken = "CONDITION";
        private const string PluginReferenceTemplate_HintPathToken = "HINT_PATH";

        private const string SupportedPlatformBuildTemplate = "SUPPORTED_PLATFORM_BUILD_CONDITION";
        private const string SupportedPlatformBuildTemplate_ConfigurationToken = "SUPPORTED_CONFIGURATION";
        private const string SupportedPlatformBuildTemplate_PlatformToken = "SUPPORTED_PLATFORM";

        private readonly FileTemplate primaryTemplateFile;
        private readonly FileTemplate propsTemplateFile;
        private readonly FileTemplate targetsTemplateFile;

        private readonly FileInfo primaryExportPath;
        private readonly FileInfo propsExportPath;
        private readonly FileInfo targetsExportPath;

        public Guid Guid { get; set; }

        public string ProjectName { get; set; }

        public bool IsGenerated { get; set; }

        public bool AllowUnsafe { get; set; }

        public string LanguageVersion { get; set; }

        public bool IsEditorOnlyProject { get; set; }

        public string DefaultPlatform { get; set; }

        public DirectoryInfo SourceIncludePath { get; set; }

        public HashSet<DirectoryInfo> SourceExcludePaths { get; } = new HashSet<DirectoryInfo>();

        public HashSet<string> SupportedPlatforms { get; } = new HashSet<string>();

        public HashSet<string> AssemblySearchPaths { get; } = new HashSet<string>();

        public Dictionary<UnityConfigurationType, HashSet<PluginReference>> PluginReferences { get; } = new Dictionary<UnityConfigurationType, HashSet<PluginReference>>();

        public Dictionary<UnityConfigurationType, HashSet<ProjectReference>> ProjectReferences { get; } = new Dictionary<UnityConfigurationType, HashSet<ProjectReference>>();

        public HashSet<ConfigurationPlatformPair> SupportedBuildPlatforms { get; } = new HashSet<ConfigurationPlatformPair>();

        public TemplatedCSharpProjectExporter(FileTemplate primaryTemplateFile, FileTemplate propsTemplateFile, FileTemplate targetsTemplateFile,
            FileInfo primaryExportPath, FileInfo propsExportPath, FileInfo targetsExportPath)
        {
            this.primaryTemplateFile = primaryTemplateFile;
            this.propsTemplateFile = propsTemplateFile;
            this.targetsTemplateFile = targetsTemplateFile;

            this.primaryExportPath = primaryExportPath;
            this.propsExportPath = propsExportPath;
            this.targetsExportPath = targetsExportPath;
        }

        public void Write()
        {
            TemplatedWriter propsWriter = new TemplatedWriter(propsTemplateFile)
                .Write(ProjectGuidToken, Guid)
                .Write(ProjectNameToken, ProjectName)
                .Write(AllowUnsafeToken, AllowUnsafe.ToString())
                .Write(LanguageVersionToken, LanguageVersion)
                .Write(IsEditorOnlyProjectToken, IsEditorOnlyProject.ToString())
                .Write(DefaultPlatformToken, DefaultPlatform)
                .Write(SupportedPlatformsToken, SupportedPlatforms)
                .Write(AssemblySearchPathsToken, AssemblySearchPaths)
                .Write(SourceIncludePathToken, SourceIncludePath.FullName);

            foreach (DirectoryInfo excludePath in SourceExcludePaths)
            {
                propsWriter.CreateWriterFor(SourceExcludeTemplate)
                    .Write(SourceExcludeTemplate_ExcludePathToken, excludePath.FullName);
            }

            foreach (KeyValuePair<UnityConfigurationType, HashSet<PluginReference>> configSet in PluginReferences)
            {
                TemplatedWriter setWriter = propsWriter.CreateWriterFor(PluginReferenceTemplateSet)
                    .Write(ReferenceTemplateSet_ConfigurationToken, configSet.Key.ToString());

                foreach (PluginReference pluginReference in configSet.Value)
                {
                    setWriter.CreateWriterFor(PluginReferenceTemplate)
                        .Write(PluginReferenceTemplate_ReferenceToken, pluginReference.ReferenceName)
                        .Write(PluginReferenceTemplate_ConditionToken, pluginReference.Condition)
                        .Write(PluginReferenceTemplate_HintPathToken, pluginReference.HintPath.LocalPath);
                }
            }

            foreach (KeyValuePair<UnityConfigurationType, HashSet<ProjectReference>> configSet in ProjectReferences)
            {
                TemplatedWriter setWriter = propsWriter.CreateWriterFor(ProjectReferenceTemplateSet)
                    .Write(ReferenceTemplateSet_ConfigurationToken, configSet.Key.ToString());

                foreach (ProjectReference pluginReference in configSet.Value)
                {
                    setWriter.CreateWriterFor(ProjectReferenceTemplate)
                        .Write(PluginReferenceTemplate_ReferenceToken, pluginReference.ReferencePath.LocalPath)
                        .Write(PluginReferenceTemplate_ConditionToken, pluginReference.Condition);
                }
            }
            propsWriter.Export(propsExportPath);

            // Write the targets part
            TemplatedWriter targetsWriter = new TemplatedWriter(targetsTemplateFile);

            foreach (ConfigurationPlatformPair pair in SupportedBuildPlatforms)
            {
                targetsWriter.CreateWriterFor(SupportedPlatformBuildTemplate)
                    .Write(SupportedPlatformBuildTemplate_ConfigurationToken, pair.Configuration)
                    .Write(SupportedPlatformBuildTemplate_PlatformToken, pair.Platform);
            }

            targetsWriter.Export(targetsExportPath);

            // Don't overwrite primary export path, as that is the file that is allowed to be edited
            if (IsGenerated)
            {
                new TemplatedWriter(primaryTemplateFile).Export(primaryExportPath);
                File.SetAttributes(primaryExportPath.FullName, FileAttributes.ReadOnly);
            }
            else if (!File.Exists(primaryExportPath.FullName))
            {
                new TemplatedWriter(primaryTemplateFile).Export(primaryExportPath);
            }
        }
    }
}
#endif