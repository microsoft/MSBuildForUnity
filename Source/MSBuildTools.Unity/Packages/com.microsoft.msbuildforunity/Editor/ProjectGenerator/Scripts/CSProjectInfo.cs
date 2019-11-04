// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// From which source was the project created.
    /// </summary>
    public enum ProjectType
    {
        /// <summary>
        /// The project is backed by an Assembly-Definition file.
        /// </summary>
        AsmDef,

        /// <summary>
        /// The project is backed by an Assembly-Definition file that only targets editor.
        /// </summary>
        EditorAsmDef,

        /// <summary>
        /// The project is one of the pre-defined editor assemblies (Assembly-CSharp-Editor, etc).
        /// </summary>
        PredefinedEditorAssembly,

        /// <summary>
        /// The project is one of the pre-defined assemblies (Assembly-CSharp, etc).
        /// </summary>
        PredefinedAssembly
    }

    /// <summary>
    /// A class representing a CSProject to be outputed.
    /// </summary>
    public class CSProjectInfo : ReferenceItemInfo
    {
        private readonly List<CSProjectDependency<CSProjectInfo>> csProjectDependencies = new List<CSProjectDependency<CSProjectInfo>>();
        private readonly List<CSProjectDependency<PluginAssemblyInfo>> pluginAssemblyDependencies = new List<CSProjectDependency<PluginAssemblyInfo>>();

        /// <summary>
        /// The associated Assembly-Definition info if available.
        /// </summary>
        public AssemblyDefinitionInfo AssemblyDefinitionInfo { get; }

        /// <summary>
        /// The type of the project.
        /// </summary>
        public ProjectType ProjectType { get; }

        /// <summary>
        /// Gets a list of project dependencies.
        /// </summary>
        public IReadOnlyCollection<CSProjectDependency<CSProjectInfo>> ProjectDependencies { get; }

        public IReadOnlyCollection<CSProjectDependency<PluginAssemblyInfo>> PluginDependencies { get; }

        /// <summary>
        /// Creates a new instance of the CSProject info.
        /// </summary>
        /// <param name="unityProjectInfo">Instance of parsed unity project info.</param>
        /// <param name="guid">The unique Guid of this reference item.</param>
        /// <param name="assemblyDefinitionInfo">The associated Assembly-Definition info.</param>
        /// <param name="assembly">The Unity assembly object associated with this csproj.</param>
        internal CSProjectInfo(UnityProjectInfo unityProjectInfo, AssemblyDefinitionInfo assemblyDefinitionInfo)
            : base(unityProjectInfo, assemblyDefinitionInfo.Guid, assemblyDefinitionInfo.Name)
        {
            AssemblyDefinitionInfo = assemblyDefinitionInfo;

            ProjectType = GetProjectType(assemblyDefinitionInfo);

            InEditorPlatforms = GetCompilationPlatforms(true);
            PlayerPlatforms = GetCompilationPlatforms(false);

            if (InEditorPlatforms.Count == 0 && PlayerPlatforms.Count == 0)
            {
                Debug.LogError($"The assembly project '{Name}' doesn't contain any supported in-editor or player platform targets.");
            }

            ProjectDependencies = new ReadOnlyCollection<CSProjectDependency<CSProjectInfo>>(csProjectDependencies);
            PluginDependencies = new ReadOnlyCollection<CSProjectDependency<PluginAssemblyInfo>>(pluginAssemblyDependencies);
        }

        private ProjectType GetProjectType(AssemblyDefinitionInfo assemblyDefinitionInfo)
        {
            if (!assemblyDefinitionInfo.IsDefaultAssembly)
            {
                return assemblyDefinitionInfo.EditorPlatformSupported && !assemblyDefinitionInfo.NonEditorPlatformSupported ? ProjectType.EditorAsmDef : ProjectType.AsmDef;
            }

            switch (assemblyDefinitionInfo.Name)
            {
                case "Assembly-CSharp":
                case "Assembly-CSharp-firstpass":
                    return ProjectType.PredefinedAssembly;
                case "Assembly-CSharp-Editor":
                case "Assembly-CSharp-Editor-firstpass":
                    return ProjectType.PredefinedEditorAssembly;
                default:
                    throw new InvalidOperationException($"Predefined assembly '{assemblyDefinitionInfo.Name}' was not recognized, this generally means it should be added to the switch statement in CSProjectInfo:GetProjectType. Treating is as a PredefinedAssembly instead of PredefinedEditorAssembly.");
            }
        }

        private ReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> GetCompilationPlatforms(bool inEditor)
        {
            // - handle all PredfinedAssemblies
            // - EditorAsmDef and PredefinedEditorAssembly for inEditor
            bool returnAllPlatforms = ProjectType == ProjectType.PredefinedAssembly
                || (inEditor && ProjectType == ProjectType.PredefinedEditorAssembly)
                || (inEditor && ProjectType == ProjectType.EditorAsmDef)
                || (inEditor && ProjectType == ProjectType.AsmDef && AssemblyDefinitionInfo.EditorPlatformSupported);

            if (returnAllPlatforms)
            {
                return new ReadOnlyDictionary<BuildTarget, CompilationPlatformInfo>(UnityProjectInfo.AvailablePlatforms.ToDictionary(t => t.BuildTarget, t => t));
            }

            // - EditorAsmDef and PredefinedEditorAssembly for not inEditor
            bool returnNoPlatforms = (!inEditor && ProjectType == ProjectType.PredefinedEditorAssembly)
                || (!inEditor && ProjectType == ProjectType.EditorAsmDef)
                || (!inEditor && ProjectType == ProjectType.AsmDef && AssemblyDefinitionInfo.TestAssembly);

            if (returnNoPlatforms)
            {
                return new ReadOnlyDictionary<BuildTarget, CompilationPlatformInfo>(new Dictionary<BuildTarget, CompilationPlatformInfo>());
            }

            // We only are an asmdef at this point, as above we handle all PredfinedAssemblies, then EditorAsmDef and PredefinedEditorAssembly for inEditor and not inEditor cases above
            Func<CompilationPlatformInfo, bool> predicate = AssemblyDefinitionInfo.includePlatforms.Length > 0
                ? predicate = (t) => AssemblyDefinitionInfo.includePlatforms.Contains(t.Name)
                : predicate = (t) => !AssemblyDefinitionInfo.excludePlatforms.Contains(t.Name);

            return new ReadOnlyDictionary<BuildTarget, CompilationPlatformInfo>(
                UnityProjectInfo.AvailablePlatforms.Where(predicate)
                    .ToDictionary(t => t.BuildTarget, t => t));
        }

        /// <summary>
        /// Adds a dependency to the project.
        /// </summary>
        /// <param name="csProjectInfo">The C# dependency.</param>
        internal void AddDependency(CSProjectInfo csProjectInfo)
        {
            AddDependency(csProjectDependencies, csProjectInfo);
        }

        /// <summary>
        /// Adds a dependency to the project.
        /// </summary>
        /// <param name="pluginAssemblyInfo">The plugin dependency.</param>
        internal void AddDependency(PluginAssemblyInfo pluginAssemblyInfo)
        {
            AddDependency(pluginAssemblyDependencies, pluginAssemblyInfo);
        }

        private void AddDependency<T>(List<CSProjectDependency<T>> items, T referenceInfo) where T : ReferenceItemInfo
        {
            items.Add(new CSProjectDependency<T>(referenceInfo,
                new HashSet<BuildTarget>(InEditorPlatforms.Keys.Intersect(referenceInfo.InEditorPlatforms.Keys)),
                new HashSet<BuildTarget>(PlayerPlatforms.Keys.Intersect(referenceInfo.PlayerPlatforms.Keys))));
        }


    }
}
#endif
