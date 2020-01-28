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
        private const string MSB4UGeneratedNote = "msb4u.generated";

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
        private const string ProjectTemplate_ProjectTypeGuidToken = "PROJECT_TYPE_GUID";
        private const string ProjectTemplate_NameToken = "PROJECT_NAME";
        private const string ProjectTemplate_GuidToken = "PROJECT_GUID";
        private const string ProjectTemplate_RelativePathToken = "PROJECT_RELATIVE_PATH";

        private const string ProjectTemplate_ProjectSection = "PROJECT_SECTION";

        private const string ProjectTemplate_ProjectSection_DependencyTemplate = "PROJECT_DEPENDENCY";
        private const string ProjectTemplate_ProjectSection_DependencyTemplate_DependencyGuidToken = "DEPENDENCY_GUID";

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

        public IDictionary<Guid, SolutionProject> Projects { get; } = new SortedDictionary<Guid, SolutionProject>();

        public IDictionary<Guid, SolutionFolder> Folders { get; } = new Dictionary<Guid, SolutionFolder>();

        public ISet<ConfigurationPlatformPair> ConfigurationPlatforms { get; } = new SortedSet<ConfigurationPlatformPair>(ConfigurationPlatformPair.Comparer.Instance);

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public IDictionary<string, string> Notes { get; } = new Dictionary<string, string>();

        public IDictionary<string, string> ExtensibilityGlobals { get; } = new Dictionary<string, string>();

        public IDictionary<string, SolutionSection> AdditionalSections { get; set; }

        public ISet<Guid> GeneratedItems { get; } = new SortedSet<Guid>();

        public TemplatedSolutionExporter(ILogger logger, FileTemplate solutionFileTemplate, FileInfo solutionOutputPath)
        {
            this.logger = logger;
            this.solutionFileTemplate = solutionFileTemplate;
            this.solutionOutputPath = solutionOutputPath;
        }

        public void Write()
        {
            TemplatedWriter writer = new TemplatedWriter(solutionFileTemplate);

            // Validate configuration, to ensure the solution has all of the children mapping
            ValidateProjectConfiguration();

            // Write out Project Data
            foreach (Guid projectGuid in GetOrderedProjects())
            {
                WriteProject(writer, projectGuid, Projects[projectGuid]);
            }

            SortedDictionary<Guid, Guid> childToParentMapping = new SortedDictionary<Guid, Guid>();
            // Write folders
            foreach (KeyValuePair<Guid, SolutionFolder> folder in Folders)
            {
                writer.CreateWriterFor(FolderTemplate)
                    .Write(FolderTemplate_GuidToken, folder.Key)
                    .Write(FolderTemplate_NameToken, folder.Value.Name);

                foreach (Guid child in folder.Value.Children)
                {
                    childToParentMapping[child] = folder.Key;
                }
            }

            // Write the sorted nested projects
            foreach (KeyValuePair<Guid, Guid> mapping in childToParentMapping)
            {
                writer.CreateWriterFor(FolderNestedTemplate)
                .Write(FolderNestedTemplate_ChildGuidToken, mapping.Key)
                .Write(FolderNestedTemplate_FolderGuidToken, mapping.Value);
            }

            // Write Config mappings
            foreach (ConfigurationPlatformPair configPair in ConfigurationPlatforms)
            {
                writer.CreateWriterFor(ConfigurationPlatformTemplate)
                    .Write(ConfigurationPlatformTemplate_ConfigurationToken, configPair.Configuration)
                    .Write(ConfigurationPlatformTemplate_PlatformToken, configPair.Platform);
            }

            // Write generated items
            WriteDictionarySet(writer, GeneratedItems.ToDictionary(guid => $"{{{guid}}}", t => MSB4UGeneratedNote), SolutionNotesTemplate);

            // Write all the known dictionary sections
            WriteDictionarySet(writer, Properties, SolutionPropertiesTemplate);
            WriteDictionarySet(writer, Notes, SolutionNotesTemplate);
            WriteDictionarySet(writer, ExtensibilityGlobals, ExtensibilityGlobalsTemplate);

            // Write Extra sections
            WriteExtraSections(writer, AdditionalSections, KnownSolutionSectionNames, ExtraGlobalSectionTemplate, t => t == SectionType.PreSection ? "preSolution" : "postSolution");

            writer.Export(solutionOutputPath);
        }

        private void ValidateProjectConfiguration()
        {
            foreach (SolutionProject project in Projects.Values)
            {
                ISet<string> configurations = new HashSet<string>(), platforms = new HashSet<string>();

                // Ensure the solution has the configured pair
                foreach (ConfigurationPlatformPair configPlatform in project.ConfigurationPlatformMapping.Keys)
                {
                    configurations.Add(configPlatform.Configuration);
                    platforms.Add(configPlatform.Platform);

                    if (!ConfigurationPlatforms.Contains(configPlatform))
                    {
                        logger?.LogWarning(nameof(TemplatedSolutionExporter), $"Configuration mapping for project '{project.Name}' {configPlatform.Configuration}|{configPlatform.Platform} isn't part of the general solution config map. Adding it.");
                        ConfigurationPlatforms.Add(configPlatform);
                    }
                }

                // Now go through the pairs and ensure we add the appropriate default to the project
                string GetOption(ISet<string> set, string targetItem) => set.Contains(targetItem) ? targetItem : (set.FirstOrDefault() ?? targetItem);

                foreach (ConfigurationPlatformPair configPlatform in ConfigurationPlatforms)
                {
                    if (!project.ConfigurationPlatformMapping.ContainsKey(configPlatform))
                    {
                        ConfigurationPlatformPair projectConfigPlatform = new ConfigurationPlatformPair(GetOption(configurations, configPlatform.Configuration), GetOption(platforms, configPlatform.Platform));
                        project.ConfigurationPlatformMapping.Add(configPlatform, new ProjectConfigurationPlatformMapping()
                        {
                            ConfigurationPlatform = projectConfigPlatform,
                            EnabledForBuild = false
                        });
                    }
                }
            }
        }

        private void WriteDictionarySet(TemplatedWriter writer, IDictionary<string, string> set, string template)
        {
            foreach (KeyValuePair<string, string> pair in set)
            {
                writer.CreateWriterFor(template)
                    .Write(KeyValueTemplate_KeyToken, pair.Key)
                    .Write(KeyValueTemplate_ValueToken, pair.Value);
            }
        }

        private void WriteProject(TemplatedWriter solutionWriter, Guid projectGuid, SolutionProject project)
        {
            TemplatedWriter projectWriter = solutionWriter.CreateWriterFor(ProjectTemplate)
                    .Write(ProjectTemplate_ProjectTypeGuidToken, project.TypeGuid)
                    .Write(ProjectTemplate_NameToken, project.Name)
                    .Write(ProjectTemplate_RelativePathToken, project.RelativePath.IsAbsoluteUri ? project.RelativePath.LocalPath : project.RelativePath.AsRelativePath())
                    .Write(ProjectTemplate_GuidToken, projectGuid);

            if (project.Dependencies.Count > 0)
            {
                TemplatedWriter dependencyWriter = projectWriter.CreateWriterFor(ProjectTemplate_ProjectSection);

                foreach (Guid dependency in project.Dependencies)
                {
                    dependencyWriter
                        .CreateWriterFor(ProjectTemplate_ProjectSection_DependencyTemplate)
                        .Write(ProjectTemplate_ProjectSection_DependencyTemplate_DependencyGuidToken, dependency);
                }
            }

            foreach (ConfigurationPlatformPair configPlatform in ConfigurationPlatforms)
            {
                ProjectConfigurationPlatformMapping mapping = project.ConfigurationPlatformMapping[configPlatform];

                WriteConfigurationProperty(solutionWriter, projectGuid, configPlatform, "ActiveCfg", mapping.ConfigurationPlatform);
                if (mapping.EnabledForBuild)
                {
                    WriteConfigurationProperty(solutionWriter, projectGuid, configPlatform, "Build.0", mapping.ConfigurationPlatform);
                }

                if (mapping.AdditionalPropertyMappings != null)
                {
                    foreach (KeyValuePair<string, ConfigurationPlatformPair> propertyPair in mapping.AdditionalPropertyMappings)
                    {
                        if (KnownConfiguationPlatformProperties.Contains(propertyPair.Key))
                        {
                            logger?.LogError(nameof(TemplatedSolutionExporter), $"Can't export '{propertyPair.Key}' as a property for project '{project.Name}' {configPlatform.Configuration}|{configPlatform.Platform}, as it's a known property.");
                        }
                        else
                        {
                            WriteConfigurationProperty(solutionWriter, projectGuid, configPlatform, propertyPair.Key, propertyPair.Value);
                        }
                    }
                }
            }

            WriteExtraSections(projectWriter, project.AdditionalSections, KnownProjectSectionNames, ExtraProjectSectionTemplate, t => t == SectionType.PreSection ? "preProject" : "postProject");
        }

        private void WriteConfigurationProperty(TemplatedWriter solutionWriter, Guid projectGuid, ConfigurationPlatformPair solutionPair, string property, ConfigurationPlatformPair projectPair)
        {
            solutionWriter.CreateWriterFor(ProjectConfigurationPlatformPropertyTemplate)
                .Write(ProjectConfigurationPlatformPropertyTemplate_ProjectGuidToken, projectGuid)
                .Write(ProjectConfigurationPlatformPropertyTemplate_SolutionConfigurationToken, solutionPair.Configuration)
                .Write(ProjectConfigurationPlatformPropertyTemplate_SolutionPlatformToken, solutionPair.Platform)
                .Write(ProjectConfigurationPlatformPropertyTemplate_PropertyToken, property)
                .Write(ProjectConfigurationPlatformPropertyTemplate_ProjectConfigurationToken, projectPair.Configuration)
                .Write(ProjectConfigurationPlatformPropertyTemplate_ProjectPlatformToken, projectPair.Platform);
        }

        private void WriteExtraSections(TemplatedWriter writer, IDictionary<string, SolutionSection> sections, ISet<string> knownSections, string sectionTemplate, Func<SectionType, string> sectionTypeToString)
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
            SolutionProject[] unorderedProjects = Projects.Values.ToArray();

            HashSet<Guid> returned = new HashSet<Guid>();
            while (returned.Count < unorderedProjects.Length)
            {
                bool oneRemoved = false;
                for (int i = 0; i < unorderedProjects.Length; i++)
                {
                    if (unorderedProjects[i] == null)
                    {
                        continue;
                    }

                    if (unorderedProjects[i].Dependencies.Count == 0 || unorderedProjects[i].Dependencies.All(guid => returned.Contains(guid)))
                    {
                        returned.Add(unorderedProjects[i].Guid);
                        yield return unorderedProjects[i].Guid;

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
        }
    }
}
#endif