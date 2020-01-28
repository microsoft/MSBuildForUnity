// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    public enum SectionType
    {
        PreSection,
        PostSection
    }

    public class ProjectConfigurationPlatformMapping
    {
        public ConfigurationPlatformPair ConfigurationPlatform { get; set; }

        public bool EnabledForBuild { get; set; }

        public Dictionary<string, ConfigurationPlatformPair> AdditionalPropertyMappings { get; set; }
    }

    public class SolutionProject
    {
        public static Guid CSharpProjectTypeGuid { get; } = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");

        public static Guid FolderTypeGuid { get; } = new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8");

        public Guid Guid { get; set; }

        public Guid TypeGuid { get; set; }

        public string Name { get; set; }

        public Uri RelativePath { get; set; }

        public HashSet<Guid> Dependencies { get; set; }

        public Dictionary<string, SolutionSection> AdditionalSections { get; set; }

        public Dictionary<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping> ConfigurationPlatformMapping { get; } = new Dictionary<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping>();
    }

    public class SolutionFolder
    {
        public string Name { get; }

        public HashSet<Guid> Children { get; } = new HashSet<Guid>();

        public SolutionFolder(string name)
        {
            Name = name;
        }
    }

    public class SolutionSection
    {
        public string Name { get; set; }

        public SectionType Type { get; set; }

        public List<string> SectionLines { get; } = new List<string>();
    }

    public interface ISolutionExporter
    {
        Dictionary<Guid, SolutionProject> Projects { get; }

        Dictionary<Guid, SolutionFolder> Folders { get; }

        HashSet<ConfigurationPlatformPair> ConfigurationPlatforms { get; }

        Dictionary<string, string> Properties { get; }

        Dictionary<string, string> Notes { get; }

        Dictionary<string, string> ExtensibilityGlobals { get; }

        Dictionary<string, SolutionSection> AdditionalSections { get; set; }

        void Write();
    }
}
#endif