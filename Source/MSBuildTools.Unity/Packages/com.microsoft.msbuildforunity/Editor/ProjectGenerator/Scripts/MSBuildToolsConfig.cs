// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    public class MSBuildToolsConfig
    {
        private const int CurrentConfigVersion = 3;

        private static string MSBuildSettingsFilePath { get; } = Path.Combine(Utilities.ProjectPath, "MSBuild", "settings.json");

        [SerializeField]
        private int version = 0;

        [FormerlySerializedAs("autoGenerateEnabled")]
        [SerializeField]
        private bool fullGenerationEnabled = false;

        [SerializeField]
        private string dependenciesProjectGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpEditorGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpFirstPassGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string assemblyCSharpFirstPassEditorGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string builtInPackagesFolderGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string importedPackagesFolderGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string externalPackagesFolderGuid = Guid.NewGuid().ToString();

        [SerializeField]
        private string solutionGuid = Guid.NewGuid().ToString();

        public bool FullGenerationEnabled
        {
            get => fullGenerationEnabled;
            set
            {
                fullGenerationEnabled = value;
                Save();
            }
        }

        internal Guid DependenciesProjectGuid { get; private set; }

        internal Guid AssemblyCSharpGuid { get; private set; }

        internal Guid AssemblyCSharpEditorGuid { get; private set; }

        internal Guid AssemblyCSharpFirstPassGuid { get; private set; }

        internal Guid AssemblyCSharpFirstPassEditorGuid { get; private set; }

        internal Guid BuiltInPackagesFolderGuid { get; private set; }

        internal Guid ImportedPackagesFolderGuid { get; private set; }

        internal Guid ExternalPackagesFolderGuid { get; private set; }

        internal Guid SolutionGuid { get; private set; }

        private void Save()
        {
            // Ensure directory exists first
            Directory.CreateDirectory(Path.GetDirectoryName(MSBuildSettingsFilePath));
            File.WriteAllText(MSBuildSettingsFilePath, EditorJsonUtility.ToJson(this));
        }

        public static MSBuildToolsConfig Load()
        {
            MSBuildToolsConfig toReturn = new MSBuildToolsConfig();

            if (File.Exists(MSBuildSettingsFilePath))
            {
                EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(MSBuildSettingsFilePath), toReturn);
            }

            bool needToSave = false;

            toReturn.DependenciesProjectGuid = EnsureGuid(ref toReturn.dependenciesProjectGuid, ref needToSave);

            toReturn.AssemblyCSharpGuid = EnsureGuid(ref toReturn.assemblyCSharpGuid, ref needToSave);
            toReturn.AssemblyCSharpEditorGuid = EnsureGuid(ref toReturn.assemblyCSharpEditorGuid, ref needToSave);
            toReturn.AssemblyCSharpFirstPassGuid = EnsureGuid(ref toReturn.assemblyCSharpFirstPassGuid, ref needToSave);
            toReturn.AssemblyCSharpFirstPassEditorGuid = EnsureGuid(ref toReturn.assemblyCSharpFirstPassEditorGuid, ref needToSave);

            toReturn.BuiltInPackagesFolderGuid = EnsureGuid(ref toReturn.builtInPackagesFolderGuid, ref needToSave);
            toReturn.ImportedPackagesFolderGuid = EnsureGuid(ref toReturn.importedPackagesFolderGuid, ref needToSave);
            toReturn.ExternalPackagesFolderGuid = EnsureGuid(ref toReturn.externalPackagesFolderGuid, ref needToSave);

            toReturn.SolutionGuid = EnsureGuid(ref toReturn.solutionGuid, ref needToSave);

            if (CurrentConfigVersion > toReturn.version)
            {
                toReturn.version = CurrentConfigVersion;
                needToSave = true;
            }

            if (needToSave)
            {
                toReturn.Save();
            }

            return toReturn;
        }

        private static Guid EnsureGuid(ref string field, ref bool needToSave)
        {
            if (!Guid.TryParse(field, out Guid guid))
            {
                guid = Guid.NewGuid();
                field = guid.ToString();

                needToSave = true;
            }

            return guid;
        }
    }
}
#endif
