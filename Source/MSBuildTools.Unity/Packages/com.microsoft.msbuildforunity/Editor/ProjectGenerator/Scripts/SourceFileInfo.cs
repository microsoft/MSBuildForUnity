// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using UnityEditor;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// Parsed information for a source file.
    /// </summary>
    public class SourceFileInfo
    {
        /// <summary>
        /// Parses the source file at a given path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="classType"></param>
        /// <returns></returns>

        private readonly string assetDatabasePath;

        private bool parsed = false;
        private Guid guid;
        private Type classType;

        /// <summary>
        /// Gets the file on disk.
        /// </summary>
        public FileInfo File { get; }

        /// <summary>
        /// Gets the Asset Guid for this source file.
        /// </summary>
        public Guid Guid
        {
            get
            {
                if (!parsed)
                {
                    Parse();
                }

                return guid;
            }
        }

        /// <summary>
        /// Gets the asset location of this source file.
        /// </summary>
        public AssetLocation AssetLocation { get; }

        /// <summary>
        /// Gets the class type of this source file. May be null, if the file was not inside the Unity project.
        /// </summary>
        public Type ClassType
        {
            get
            {
                if (!parsed)
                {
                    Parse();
                }
                return classType;
            }
        }

        public SourceFileInfo(FileInfo fileInfo, AssetLocation assetLocation, string assetDatabasePath)
        {
            this.assetDatabasePath = assetDatabasePath;

            if (fileInfo.Extension != ".cs")
            {
                throw new ArgumentException($"Given file '{fileInfo.FullName}' is not a C# source file.");
            }

            File = fileInfo;
            AssetLocation = assetLocation;
        }

        private void Parse()
        {
            if (!Guid.TryParse(AssetDatabase.AssetPathToGUID(assetDatabasePath), out guid) && !Utilities.TryGetGuidForAsset(File, out guid))
            {
                throw new InvalidOperationException($"Couldn't get guid for source asset '{File.FullName}'.");
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(File.FullName);
            classType = script?.GetClass();

            parsed = true;
        }
    }
}
