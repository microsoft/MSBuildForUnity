// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// Type of Plugin.
    /// </summary>
    public enum PluginType
    {
        /// <summary>
        /// A .NET DLL.
        /// </summary>
        Managed,

        /// <summary>
        /// A native (C++) dll.
        /// </summary>
        Native
    }

    /// <summary>
    /// This is the information for the plugins in the Unity project.
    /// </summary>
    public class PluginAssemblyInfo : ReferenceItemInfo
    {
        private const string EditorPlatformName = "Editor";

        /// <summary>
        /// Gets the type of Plugin
        /// </summary>
        public PluginType Type { get; }

        /// <summary>
        /// Gets whether this plugin is auto referenced, as in whether the generated projects will automatically reference this plugin.
        /// </summary>
        public bool AutoReferenced { get; private set; }

        /// <summary>
        /// Gets the output path to the reference.
        /// </summary>
        public Uri ReferencePath { get; }

        /// <summary>
        /// If the plugin has define constraints, then it will only be referenced if the platform/project defines at least one of these constraints.
        /// ! operator means that the specified plugin must not be included
        /// https://docs.unity3d.com/ScriptReference/PluginImporter.DefineConstraints.html
        /// </summary>
        public HashSet<string> DefineConstraints { get; private set; }

        /// <summary>
        /// Creates a new instance of the <see cref="PluginAssemblyInfo"/>.
        /// </summary>
        public PluginAssemblyInfo(UnityProjectInfo unityProjectInfo, Guid guid, string fullPath, PluginType type)
             : base(unityProjectInfo, guid, Path.GetFileNameWithoutExtension(fullPath))
        {
            Type = type;
            ReferencePath = new Uri(fullPath);

            if (Type == PluginType.Managed)
            {
                ParseYAMLFile();
            }
        }

        private void ParseYAMLFile()
        {
            // This approach doesn't work for native YAML parsing

            Dictionary<string, bool> enabledPlatforms = new Dictionary<string, bool>();
            using (StreamReader reader = new StreamReader(ReferencePath.LocalPath + ".meta"))
            {
                DefineConstraints = new HashSet<string>();

                // Parse define constraints
                string defineConstraints = reader.ReadUntil("defineConstraints:", "isExplicitlyReferenced:", "platformData:");
                string isExplicitlyReferenced;
                if (defineConstraints.Contains("defineConstraints:"))
                {
                    // Match anything, then '[', then match a group of anything then ']' than anything
                    string inLineEntryPattern = @"[^\[]*\[([^\]]*)\][^\]]*";
                    Match match = Regex.Match(defineConstraints, inLineEntryPattern);
                    if (match.Success && string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        // No define constraints
                    }
                    else if (match.Success) // We have non-empty string in the contents of the group we matched; NUnit has this:  defineConstraints: ["UNITY_INCLUDE_TESTS"]
                    {
                        // Yaml kinda allows this
                        string[] defines = match.Groups[1].Value.Trim().Split(',');
                        foreach (string define in defines)
                        {
                            ProcessDefineEntry(define);
                        }
                    }
                    else
                    {
                        reader.ReadWhile(line =>
                        {
                            line = line.Trim();
                            if (line.StartsWith("-"))
                            {
                                ProcessDefineEntry(line.Substring(1));
                                return true;
                            }
                            // else
                            return false;
                        });
                    }

                    // Since succeded, read until isExplicitlyReferenced or platformData
                    isExplicitlyReferenced = reader.ReadUntil("isExplicitlyReferenced:", "platformData:");
                }
                else
                {
                    // If it's not defineConstraints, then it's one of the other 3
                    isExplicitlyReferenced = defineConstraints;
                }

                if (isExplicitlyReferenced.Contains("isExplicitlyReferenced:"))
                {
                    AutoReferenced = isExplicitlyReferenced.Split(':')[1].Trim().Equals("0");
                }
                else
                {
                    // Is default true?
                    AutoReferenced = true;
                }

                if (!isExplicitlyReferenced.Contains("platformData:"))
                {
                    // Read until platform data
                    reader.ReadUntil("platformData:");
                }

                ParsePlatformData(reader, enabledPlatforms);
            }

            Dictionary<BuildTarget, CompilationPlatformInfo> inEditorPlatforms = new Dictionary<BuildTarget, CompilationPlatformInfo>();
            if (enabledPlatforms.TryGetValue(EditorPlatformName, out bool platformEnabled) && platformEnabled)
            {
                foreach (CompilationPlatformInfo platform in UnityProjectInfo.AvailablePlatforms)
                {
                    inEditorPlatforms.Add(platform.BuildTarget, platform);
                }
            }

            Dictionary<BuildTarget, CompilationPlatformInfo> playerPlatforms = new Dictionary<BuildTarget, CompilationPlatformInfo>();

            foreach (KeyValuePair<BuildTarget, string> pair in MSBuildTools.SupportedBuildTargets)
            {
                TryAddEnabledPlatform(playerPlatforms, enabledPlatforms, pair.Value, pair.Key);
            }

            FilterPlatformsBasedOnDefineConstraints(inEditorPlatforms, true);
            FilterPlatformsBasedOnDefineConstraints(playerPlatforms, false);

            InEditorPlatforms = new ReadOnlyDictionary<BuildTarget, CompilationPlatformInfo>(inEditorPlatforms);
            PlayerPlatforms = new ReadOnlyDictionary<BuildTarget, CompilationPlatformInfo>(playerPlatforms);
        }

        private void ProcessDefineEntry(string defineEntry)
        {
            string define = defineEntry.Trim();

            if ((define.StartsWith("'") && define.EndsWith("'"))
                || (define.StartsWith("\"") && define.EndsWith("\"")))
            {
                define = define.Substring(1, define.Length - 2);
            }

            DefineConstraints.Add(define);
        }

        private void ParsePlatformData(StreamReader reader, Dictionary<string, bool> enabledPlatforms)
        {
            if (reader.ReadUntil("first:", "userData:").Contains("userData:") || reader.EndOfStream)
            {
                // We reached the end
                return;
            }
            string nextLine = reader.ReadLine();
            if (nextLine.Contains("'': Any")) // Try use exclude method
            {
                string settingsLine = reader.ReadUntil("settings:", "userData:");
                if (settingsLine.Contains("userData:"))
                {
                    return;
                }

                // We are fine to use exclude method if we have a set of settings
                if (!settingsLine.Contains("settings: {}"))
                {
                    // We need to add all
                    SetAllPlatformsEnabled(enabledPlatforms);

                    reader.ReadWhile(l =>
                    {
                        if (l.Contains("Exclude"))
                        {
                            string[] parts = l.Trim().Replace("Exclude ", string.Empty).Split(':');
                            bool isExclude = parts[1].Trim() == "1";
                            if (isExclude)
                            {
                                enabledPlatforms.Remove(parts[0]);
                            }
                            return true;
                        }

                        return false;
                    });

                    return;
                }
            }
            // else fall through to use -first method 

            string line;
            while ((line = reader.ReadUntil("first:", "userData:")).Contains("first:") && !reader.EndOfStream)
            {
                string[] platformLineParts = reader.ReadLine().Split(':');
                string platform = platformLineParts[1].Trim();

                if (platformLineParts[0].Contains("Facebook"))
                {
                    platform = $"Facebook{platform}";
                }
                string enabledLine = reader.ReadUntil("enabled:");

                bool isEnabled = enabledLine.Split(':')[1].Trim() == "1";

                if (isEnabled)
                {
                    if (platformLineParts[0].Trim().Equals("Any"))
                    {
                        // All platforms are indeed enabled
                        SetAllPlatformsEnabled(enabledPlatforms);
                        return;
                    }
                    else
                    {
                        enabledPlatforms.Add(platform, isEnabled);
                    }
                }
            }
        }

        private void SetAllPlatformsEnabled(Dictionary<string, bool> enabledPlatforms)
        {
            foreach (CompilationPlatformInfo platform in UnityProjectInfo.AvailablePlatforms)
            {
                enabledPlatforms.Add(MSBuildTools.SupportedBuildTargets[platform.BuildTarget], true);
            }

            enabledPlatforms.Add(EditorPlatformName, true);
        }

        private bool ContainsDefineHelper(string define, bool inEditor, CompilationPlatformInfo platform)
        {
            return platform.CommonPlatformDefines.Contains(define)
                || (inEditor ? platform.AdditionalInEditorDefines.Contains(define) : platform.AdditionalPlayerDefines.Contains(define));
        }

        private void FilterPlatformsBasedOnDefineConstraints(IDictionary<BuildTarget, CompilationPlatformInfo> platformDictionary, bool inEditor)
        {
            if (DefineConstraints.Count == 0)
            {
                // No exclusions
                return;
            }

            bool defaultExcludeValue = DefineConstraints.Any(t => !t.StartsWith("!"));
            HashSet<BuildTarget> toExclude = new HashSet<BuildTarget>();
            foreach (KeyValuePair<BuildTarget, CompilationPlatformInfo> platformPair in platformDictionary)
            {
                // We presume exclude, then check
                bool exclude = defaultExcludeValue;
                foreach (string define in DefineConstraints)
                {
                    // Does this define exclude
                    if (define.StartsWith("!"))
                    {
                        if (ContainsDefineHelper(define.Substring(1), inEditor, platformPair.Value))
                        {
                            exclude = true;
                            break;
                        }
                    }
                    else if (ContainsDefineHelper(define, inEditor, platformPair.Value))
                    {
                        // This platform is supported, but still search for !defineconstraitns that may force exclusion
                        exclude = false;
                    }
                }

                if (exclude)
                {
                    toExclude.Add(platformPair.Key);
                }
            }

            foreach (BuildTarget buildTarget in toExclude)
            {
                platformDictionary.Remove(buildTarget);
            }
        }

        private void TryAddEnabledPlatform(Dictionary<BuildTarget, CompilationPlatformInfo> playerPlatforms, Dictionary<string, bool> enabledPlatforms, string platformName, BuildTarget platformTarget)
        {
            if (enabledPlatforms.TryGetValue(platformName, out bool platformEnabled) && platformEnabled)
            {
                CompilationPlatformInfo platform = UnityProjectInfo.AvailablePlatforms.FirstOrDefault(t => t.BuildTarget == platformTarget);
                if (platform != null)
                {
                    playerPlatforms.Add(platformTarget, platform);
                }
                else
                {
                    Debug.LogError($"Platform '{platformName}' was specified as enabled by '{ReferencePath.LocalPath}' plugin, but not available in processed compilation settings.");
                }
            }
        }
    }
}
#endif