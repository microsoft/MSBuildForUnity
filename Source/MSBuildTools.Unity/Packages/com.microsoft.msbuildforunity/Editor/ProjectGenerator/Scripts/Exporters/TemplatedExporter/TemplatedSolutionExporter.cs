// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using Microsoft.Build.Unity.ProjectGeneration.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters.TemplatedExporter
{
    public class TemplatedSolutionExporter : ISolutionExporter
    {
        private static readonly HashSet<string> KnownProjectSectionNames = new HashSet<string>()
        {
            "ProjectDependencies"
        };

        private static readonly HashSet<string> KnownSolutionSectionNames = new HashSet<string>()
        {
            "SolutionConfigurationPlatforms",
            "ProjectConfigurationPlatforms",
            "SolutionProperties",
            "NestedProjects",
            "ExtensibilityGlobals",
            "SolutionNotes"
        };

        private static readonly HashSet<string> KnownConfiguationPlatformProperties = new HashSet<string>()
        {
            "ActiveCfg",
            "Build.0"
        };

        #region Templates & Tokens
        private const string ProjectTemplate = "PROJECT";
        private const string ProjectTemplate_NameToken = "PROJECT_NAME";
        private const string ProjectTemplate_GuidToken = "PROJECT_GUID";
        private const string ProjectTemplate_RelativePathToken = "PROJECT_RELATIVE_PATH";

        private const string ProjectTemplate_DependencyTemplate = "PROJECT_DEPENDENCY";
        private const string ProjectTemplate_DependencyTemplate_DependencyGuidToken = "DEPENDENCY_GUID";

        private const string FolderTemplate = "FOLDER";
        private const string FolderTemplate_NameToken = "FOLDER_NAME";
        private const string FolderTemplate_GuidToken = "FOLDER_GUID";

        private const string ConfigurationPlatformTemplate = "CONFIGURATION_PLATFORM";
        private const string ConfigurationPlatformTemplate_ConfigurationToken = "CONFIGURATION";
        private const string ConfigurationPlatformTemplate_PlatformToken = "PLATFORM";

        private const string ProjectConfigurationPlatformPropertyTemplate = "CONFIGURATION_PLATFORM_PROPERTY";
        private const string ProjectConfigurationPlatformPropertyTemplate_ProjectGuidToken = "PROJECT_GUID";
        private const string ProjectConfigurationPlatformPropertyTemplate_SolutionConfigurationToken = "SOLUTION_CONFIGURATION";
        private const string ProjectConfigurationPlatformPropertyTemplate_SolutionPlatformToken = "SOLUTION_PLATFORM";
        private const string ProjectConfigurationPlatformPropertyTemplate_PropertyToken = "PROPERTY";
        private const string ProjectConfigurationPlatformPropertyTemplate_ProjectConfigurationToken = "PROJECT_CONFIGURATION";
        private const string ProjectConfigurationPlatformPropertyTemplate_ProjectPlatformToken = "PROJECT_PLATFORM";

        private const string SolutionPropertiesTemplate = "SOLUTION_PROPERTIES";
        private const string ExtensibilityGlobalsTemplate = "EXTENSIBILITY_GLOBALS";
        private const string SolutionNotesTemplate = "SOLUTION_NOTES";
        private const string KeyValueTemplate_KeyToken = "PROPERTY_KEY";
        private const string KeyValueTemplate_ValueToken = "PROPERTY_VALUE";

        private const string FolderNestedTemplate = "FOLDER_NESTED_PROJECTS";
        private const string FolderNestedTemplate_ChildGuidToken = "CHILD_GUID";
        private const string FolderNestedTemplate_FolderGuidToken = "FOLDER_GUID";

        private const string ExtraProjectSectionTemplate = "EXTRA_PROJECT_SECTION";
        private const string ExtraGlobalSectionTemplate = "EXTRA_GLOBAL_SECTION";
        private const string ExtraSectionTemplate_SectionNameToken = "SECTION_NAME";
        private const string ExtraSectionTemplate_SectionTypeToken = "PRE_POST_SECTION";

        private const string ExtraSectionTemplate_LineTemplate = "EXTRA_SECTION_LINE";
        private const string ExtraSectionTemplate_LineTemplate_LineToken = "SECTION_LINE";
        #endregion

        private readonly ILogger logger;
        private readonly FileTemplate solutionFileTemplate;
        private readonly FileInfo solutionOutputPath;

        public Dictionary<Guid, SolutionProject> Projects { get; } = new Dictionary<Guid, SolutionProject>();

        public Dictionary<Guid, SolutionFolder> Folders { get; } = new Dictionary<Guid, SolutionFolder>();

        public HashSet<ConfigurationPlatformPair> ConfigurationPlatforms { get; } = new HashSet<ConfigurationPlatformPair>();

        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> Notes { get; } = new Dictionary<string, string>();

        public Dictionary<string, string> ExtensibilityGlobals { get; } = new Dictionary<string, string>();

        public Dictionary<string, SolutionSection> AdditionalSections { get; set; }

        public TemplatedSolutionExporter(ILogger logger, FileTemplate solutionFileTemplate, FileInfo solutionOutputPath)
        {
            this.logger = logger;
            this.solutionFileTemplate = solutionFileTemplate;
            this.solutionOutputPath = solutionOutputPath;
        }

        public void Write()
        {
            TemplatedWriter writer = new TemplatedWriter(solutionFileTemplate);

            TemplatedWriter configMappingsWriter = writer.CreateWriterFor(ProjectConfigurationPlatformPropertyTemplate);

            IEnumerable<Guid> orderedProjectSet = GetOrderedProjects();

            // Write out Project Data
            foreach (Guid projectGuid in orderedProjectSet)
            {
                WriteProject(writer, configMappingsWriter, projectGuid, Projects[projectGuid]);
            }

            // Write folders
            foreach (KeyValuePair<Guid, SolutionFolder> folder in Folders)
            {
                writer.CreateWriterFor(FolderTemplate)
                    .Write(FolderTemplate_GuidToken, folder.Key)
                    .Write(FolderTemplate_NameToken, folder.Value.Name);

                foreach (Guid child in folder.Value.Children)
                {
                    writer.CreateWriterFor(FolderNestedTemplate)
                        .Write(FolderNestedTemplate_FolderGuidToken, folder.Key)
                        .Write(FolderNestedTemplate_ChildGuidToken, child);
                }
            }

            // Write Config mappings
            foreach (ConfigurationPlatformPair configPair in ConfigurationPlatforms)
            {
                writer.CreateWriterFor(ConfigurationPlatformTemplate)
                    .Write(ConfigurationPlatformTemplate_ConfigurationToken, configPair.Configuration)
                    .Write(ConfigurationPlatformTemplate_PlatformToken, configPair.Platform);
            }

            // Write all the known dictionary sections
            WriteDictionarySet(writer, Properties, SolutionPropertiesTemplate);
            WriteDictionarySet(writer, Notes, SolutionNotesTemplate);
            WriteDictionarySet(writer, ExtensibilityGlobals, ExtensibilityGlobalsTemplate);

            // Write Extra sections
            WriteExtraSections(writer, AdditionalSections, KnownSolutionSectionNames, ExtraGlobalSectionTemplate, t => t == SectionType.PreSection ? "preSolution" : "postSolution");
        }

        private void WriteDictionarySet(TemplatedWriter writer, Dictionary<string, string> set, string template)
        {
            foreach (KeyValuePair<string, string> pair in set)
            {
                writer.CreateWriterFor(template)
                    .Write(KeyValueTemplate_KeyToken, pair.Key)
                    .Write(KeyValueTemplate_ValueToken, pair.Value);
            }
        }

        private void WriteProject(TemplatedWriter solutionWriter, TemplatedWriter configMappingWriter, Guid projectGuid, SolutionProject project)
        {
            TemplatedWriter projectWriter = solutionWriter.CreateWriterFor(ProjectTemplate)
                    .Write(ProjectTemplate_NameToken, project.Name)
                    .Write(ProjectTemplate_RelativePathToken, project.RelativePath.IsAbsoluteUri ? project.RelativePath.LocalPath : project.RelativePath.AsRelativePath())
                    .Write(ProjectTemplate_GuidToken, projectGuid);

            foreach (Guid dependency in project.Dependencies)
            {
                projectWriter.CreateWriterFor(ProjectTemplate_DependencyTemplate)
                    .Write(ProjectTemplate_DependencyTemplate_DependencyGuidToken, dependency);
            }

            foreach (KeyValuePair<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping> config in project.ConfigurationPlatformMapping)
            {
                if (!ConfigurationPlatforms.Contains(config.Key))
                {
                    logger?.LogWarning(nameof(TemplatedSolutionExporter), $"Configuration mapping for project '{project.Name}' {config.Key.Configuration}|{config.Key.Platform} isn't part of the general solution config map. Adding it.");
                    ConfigurationPlatforms.Add(config.Key);
                }

                WriteConfigurationProperty(configMappingWriter, projectGuid, config.Key, "ActiveCfg", config.Value.ConfigurationPlatform);
                if (config.Value.EnabledForBuild)
                {
                    WriteConfigurationProperty(configMappingWriter, projectGuid, config.Key, "Build.0", config.Value.ConfigurationPlatform);
                }

                foreach (KeyValuePair<string, ConfigurationPlatformPair> propertyPair in config.Value.AdditionalPropertyMappings)
                {
                    if (KnownConfiguationPlatformProperties.Contains(propertyPair.Key))
                    {
                        logger?.LogError(nameof(TemplatedSolutionExporter), $"Can't export '{propertyPair.Key}' as a property for project '{project.Name}' {config.Key.Configuration}|{config.Key.Platform}, as it's a known property.");
                    }
                    else
                    {
                        WriteConfigurationProperty(configMappingWriter, projectGuid, config.Key, propertyPair.Key, propertyPair.Value);
                    }
                }
            }

            WriteExtraSections(projectWriter, project.AdditionalSections, KnownProjectSectionNames, ExtraProjectSectionTemplate, t => t == SectionType.PreSection ? "preProject" : "postProject");
        }

        private void WriteConfigurationProperty(TemplatedWriter writer, Guid projectGuid, ConfigurationPlatformPair solutionPair, string property, ConfigurationPlatformPair projectPair)
        {
            writer
                .Write(ProjectConfigurationPlatformPropertyTemplate_ProjectGuidToken, projectGuid)
                .Write(ProjectConfigurationPlatformPropertyTemplate_SolutionConfigurationToken, solutionPair.Configuration)
                .Write(ProjectConfigurationPlatformPropertyTemplate_SolutionPlatformToken, solutionPair.Platform)
                .Write(ProjectConfigurationPlatformPropertyTemplate_PropertyToken, property)
                .Write(ProjectConfigurationPlatformPropertyTemplate_ProjectConfigurationToken, projectPair.Configuration)
                .Write(ProjectConfigurationPlatformPropertyTemplate_ProjectPlatformToken, projectPair.Platform);
        }

        private void WriteExtraSections(TemplatedWriter writer, Dictionary<string, SolutionSection> sections, HashSet<string> knownSections, string sectionTemplate, Func<SectionType, string> sectionTypeToString)
        {
            foreach (KeyValuePair<string, SolutionSection> section in sections)
            {
                if (knownSections.Contains(section.Key))
                {
                    logger?.LogError(nameof(TemplatedSolutionExporter), $"Can't export '{section.Key}' as an additional section, as it's a known section.");
                }
                else
                {
                    TemplatedWriter extraProjectSectionWriter = writer.CreateWriterFor(sectionTemplate)
                        .Write(ExtraSectionTemplate_SectionNameToken, section.Key)
                        .Write(ExtraSectionTemplate_SectionTypeToken, sectionTypeToString(section.Value.Type));

                    foreach (string line in section.Value.SectionLines)
                    {
                        extraProjectSectionWriter.CreateWriterFor(ExtraSectionTemplate_LineTemplate)
                            .Write(ExtraSectionTemplate_LineTemplate_LineToken, line);
                    }
                }
            }
        }

        // Ordered based on dependency
        private IEnumerable<Guid> GetOrderedProjects()
        {
            SortedSet<Guid> toReturn = new SortedSet<Guid>();

            SolutionProject[] unorderedProjects = Projects.Values.ToArray();

            while (toReturn.Count < unorderedProjects.Length)
            {
                bool oneRemoved = false;
                for (int i = 0; i < unorderedProjects.Length; i++)
                {
                    if (unorderedProjects[i] == null)
                    {
                        continue;
                    }

                    if (unorderedProjects[i].Dependencies.Count == 0 || unorderedProjects[i].Dependencies.All(guid => toReturn.Contains(guid)))
                    {
                        toReturn.Add(unorderedProjects[i].Guid);

                        unorderedProjects[i] = null;
                        oneRemoved = true;
                    }
                }

                if (!oneRemoved)
                {
                    logger?.LogError(nameof(TemplatedSolutionExporter), $"Possible circular dependency.");
                    break;
                }
            }

            return toReturn;
        }
    }
}
#endif