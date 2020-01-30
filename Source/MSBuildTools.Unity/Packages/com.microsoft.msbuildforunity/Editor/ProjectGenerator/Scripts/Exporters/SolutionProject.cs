// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Build.Unity.ProjectGeneration.Exporters
{
    /// <summary>
    /// Represents a project entry that is part of the solution.
    /// </summary>
    public class SolutionProject
    {
        /// <summary>
        /// The Guid that represents a C# project type.
        /// </summary>
        public static Guid CSharpProjectTypeGuid { get; } = new Guid("9A19103F-16F7-4668-BE54-9A1E7A4F7556");

        /// <summary>
        /// Gets the guid that represents a folder type.
        /// </summary>
        public static Guid FolderTypeGuid { get; } = new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8");

        /// <summary>
        /// Gets the guid of the project.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// Gets the type of Project entry; note: <see cref="SolutionFolder"/> is for Folders.
        /// </summary>
        public Guid TypeGuid { get; }

        /// <summary>
        /// Gets the name of the project.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the relative path of the project.
        /// </summary>
        public Uri RelativePath { get; }

        /// <summary>
        /// Gets the dependency set for this project.
        /// </summary>
        public ISet<Guid> Dependencies { get; }

        /// <summary>
        /// Gets a mutable dictionary of additional sections for this project.
        /// </summary>
        public IDictionary<string, SolutionSection> AdditionalSections { get; }

        /// <summary>
        /// Gets a mutable dictionary of configuration-platfornm mappings.
        /// </summary>
        public IDictionary<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping> ConfigurationPlatformMapping { get; }
            = new SortedDictionary<ConfigurationPlatformPair, ProjectConfigurationPlatformMapping>(ConfigurationPlatformPair.Comparer.Instance);

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SolutionProject(Guid guid, Guid typeGuid, string name, string relativePath, IEnumerable<SolutionSection> additionalSections, IEnumerable<Guid> depedencies)
            : this(guid, typeGuid, name, new Uri(relativePath, UriKind.Relative), additionalSections, depedencies) { }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SolutionProject(Guid guid, Guid typeGuid, string name, string relativePath, IEnumerable<SolutionSection> additionalSections, HashSet<Guid> depedencies)
            : this(guid, typeGuid, name, new Uri(relativePath, UriKind.Relative), additionalSections, depedencies) { }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SolutionProject(Guid guid, Guid typeGuid, string name, Uri relativePath, IEnumerable<SolutionSection> additionalSections, IEnumerable<Guid> depedencies)
            : this(guid, typeGuid, name, relativePath, additionalSections, new HashSet<Guid>(depedencies)) { }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SolutionProject(Guid guid, Guid typeGuid, string name, Uri relativePath, IEnumerable<SolutionSection> additionalSections, HashSet<Guid> depedencies)
        {
            if (typeGuid == FolderTypeGuid)
            {
                throw new ArgumentException($"If you wish to specify a folder 'project type', create a {nameof(SolutionFolder)}.");
            }

            Guid = guid;
            TypeGuid = typeGuid;
            Name = name;
            RelativePath = relativePath;
            AdditionalSections = additionalSections?.ToDictionary(t => t.Name) ?? new Dictionary<string, SolutionSection>();
            Dependencies = depedencies;
        }
    }
}
#endif