// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

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

        public IDictionary<string, ConfigurationPlatformPair> AdditionalPropertyMappings { get; set; }
    }

    public class SolutionProject
    {
        public static Guid CSharpProjectTypeGuid { get; } = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");

        public static Guid FolderTypeGuid { get; } = new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8");

        public Guid Guid { get; set; }

        public Guid TypeGuid { get; set; }

        public string Name { get; set; }

        public Uri RelativePath { get; set; }

        public ISet<Guid> Dependencies { get; }

        public IDictionary<string, SolutionSection> AdditionalSections { get; }

        public IDictionary<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping> ConfigurationPlatformMapping { get; }
            = new SortedDictionary<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping>(ConfigurationPlatformPair.Comparer.Instance);

        public SolutionProject(Guid guid, Guid typeGuid, string name, string relativePath, IEnumerable<SolutionSection> additionalSections, IEnumerable<Guid> depedencies)
            : this(guid, typeGuid, name, new Uri(relativePath, UriKind.Relative), additionalSections, depedencies) { }

        public SolutionProject(Guid guid, Guid typeGuid, string name, string relativePath, IEnumerable<SolutionSection> additionalSections, HashSet<Guid> depedencies)
            : this(guid, typeGuid, name, new Uri(relativePath, UriKind.Relative), additionalSections, depedencies) { }

        public SolutionProject(Guid guid, Guid typeGuid, string name, Uri relativePath, IEnumerable<SolutionSection> additionalSections, IEnumerable<Guid> depedencies)
            : this(guid, typeGuid, name, relativePath, additionalSections, new HashSet<Guid>(depedencies)) { }

        public SolutionProject(Guid guid, Guid typeGuid, string name, Uri relativePath, IEnumerable<SolutionSection> additionalSections, HashSet<Guid> depedencies)
        {
            Guid = guid;
            TypeGuid = typeGuid;
            Name = name;
            RelativePath = relativePath;
            AdditionalSections = additionalSections?.ToDictionary(t => t.Name) ?? new Dictionary<string, SolutionSection>();
            Dependencies = depedencies;
        }
    }

    public class SolutionFolder
    {
        public string Name { get; }

        public ISet<Guid> Children { get; } = new HashSet<Guid>();

        public SolutionFolder(string name)
        {
            Name = name;
        }
    }

    public class SolutionSection
    {
        public string Name { get; set; }

        public SectionType Type { get; set; }

        public IList<string> SectionLines { get; } = new List<string>();

        public override bool Equals(object obj)
        {
            return obj is SolutionSection other
                && Equals(Name, other.Name)
                && Type == other.Type;
        }

        public override int GetHashCode()
        {
            return (Name?.GetHashCode() ?? 0) ^ Type.GetHashCode();
        }
    }

    public interface ISolutionExporter
    {
        IDictionary<Guid, SolutionProject> Projects { get; }

        IDictionary<Guid, SolutionFolder> Folders { get; }

        ISet<ConfigurationPlatformPair> ConfigurationPlatforms { get; }

        IDictionary<string, string> Properties { get; }

        ISet<Guid> GeneratedItems { get; }

        IDictionary<string, string> Notes { get; }

        IDictionary<string, string> ExtensibilityGlobals { get; }

        IDictionary<string, SolutionSection> AdditionalSections { get; set; }

        void Write();
    }
}
#endif