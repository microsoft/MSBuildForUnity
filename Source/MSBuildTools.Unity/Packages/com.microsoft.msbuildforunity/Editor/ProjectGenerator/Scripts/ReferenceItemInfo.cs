﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// A common base class for reference items such as C# Projects and DLLs to be added to MSBuild.
    /// </summary>
    public class ReferenceItemInfo
    {
        /// <summary>
        /// Gets the instance of the parsed project information.
        /// </summary>
        protected UnityProjectInfo UnityProjectInfo { get; }

        /// <summary>
        /// Gets the Guid associated with the reference.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// Gets name of the reference item.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a set of platforms supported for the InEditor configuration.
        /// </summary>
        /// <remarks>
        /// In the editor, we can support all platforms if it's a pre-defined assembly, or an asmdef with Editor platform checked. 
        /// Otherwise we fallback to just the platforms specified in the editor.
        /// </remarks>
        public IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> InEditorPlatforms { get; protected set; }

        /// <summary>
        /// Gets a set of platforms supported for the Player configuration.
        /// </summary>
        /// <remarks>
        /// In the player, we support any platform if pre-defined assembly, or the ones explicitly specified in the AsmDef player.
        /// </remarks>
        public IReadOnlyDictionary<BuildTarget, CompilationPlatformInfo> PlayerPlatforms { get; protected set; }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="unityProjectInfo">Instance of parsed unity project info.</param>
        /// <param name="guid">The unique Guid of this reference item.</param>
        /// <param name="referencePath">The output path to the reference item.</param>
        /// <param name="name">The name of the reference.</param>
        protected ReferenceItemInfo(UnityProjectInfo unityProjectInfo, Guid guid, string name)
        {
            UnityProjectInfo = unityProjectInfo;
            Guid = guid;
            Name = name;
        }

        /// <summary>
        /// A much more readable string representation of this reference item info.
        /// </summary>
        public override string ToString()
        {
            return $"{GetType().Name}: {Name}";
        }
    }
}
#endif