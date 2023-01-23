// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Microsoft.Build.Unity.ProjectGeneration
{
    /// <summary>
    /// Represents TargetFrameworks that Unity supports.
    /// </summary>
    public enum TargetFramework
    {
        NetStandard20,
        NetStandard21,
        Net20,
        Net46,
        Net471
    }

    public enum ScriptingBackend
    {
        Mono,
        Net,
        IL2CPP
    }

    /// <summary>
    /// Helper extensions for the <see cref="TargetFramework"/> enum.
    /// </summary>
    public static class TargetFrameworkExtensions
    {
        /// <summary>
        /// Converts a <see cref="TargetFramework"/> into an MSBuild acceptable string.
        /// </summary>
        /// <param name="this">The <see cref="TargetFramework"/> to convert.</param>
        /// <returns>The MSBuild acceptable string representing the <see cref="TargetFramework"/>.</returns>
        public static string AsMSBuildString(this TargetFramework @this)
        {
            switch (@this)
            {
                case TargetFramework.NetStandard20:
                    return "netstandard2.0";
                case TargetFramework.NetStandard21:
                    return "netstandard2.1";
                case TargetFramework.Net20:
                    return "net20";
                case TargetFramework.Net46:
                    return "net46";
                case TargetFramework.Net471:
                    return "net471";
            }

            throw new ArgumentOutOfRangeException(nameof(@this));
        }

        /// <summary>
        /// Converts a <see cref="TargetFramework"/> into a string our template files use..
        /// </summary>
        /// <param name="this">The <see cref="TargetFramework"/> to convert.</param>
        /// <returns>The template file acceptable string representing the <see cref="TargetFramework"/>.</returns>
        public static string AsTemplateString(this TargetFramework @this)
        {
            switch (@this)
            {
                case TargetFramework.NetStandard20:
                    return "NetStandard20";
                case TargetFramework.NetStandard21:
                    return "NetStandard21";
                case TargetFramework.Net20:
                    return "Net20";
                case TargetFramework.Net46:
                    return "Net46";
                case TargetFramework.Net471:
                    return "Net471";
            }

            throw new ArgumentOutOfRangeException(nameof(@this));
        }

        /// <summary>
        /// Returns the configured <see cref="TargetFramework"/> for the <see cref="BuildTargetGroup"/>.
        /// </summary>
        /// <param name="this">The <see cref="BuildTargetGroup"/> to get <see cref="TargetFramework"/> for.</param>
        /// <returns>The <see cref="TargetFramework"/> configured for given <see cref="BuildTargetGroup"/>.</returns>
        public static TargetFramework GetTargetFramework(this BuildTargetGroup @this)
        {
            if (@this == BuildTargetGroup.Unknown)
            {
                // This may be different on older unity versions
                return TargetFramework.Net46;
            }

            switch (PlayerSettings.GetApiCompatibilityLevel(@this))
            {
                case ApiCompatibilityLevel.NET_2_0:
                case ApiCompatibilityLevel.NET_2_0_Subset:
                    return TargetFramework.Net20;
#if !UNITY_2021_2_OR_NEWER
                case ApiCompatibilityLevel.NET_4_6:
                    return TargetFramework.Net46;
#else
                case ApiCompatibilityLevel.NET_Unity_4_8:
                    // Unity 2021.2+ and 2022.* both generates projects that targets
                    // .NET Framework 4.7.1 instead of 4.8, so we can't use net48 here.
                    return TargetFramework.Net471;
#endif
                case ApiCompatibilityLevel.NET_Web:
                case ApiCompatibilityLevel.NET_Micro:
                    throw new PlatformNotSupportedException("Don't currently support NET_Web and NET_Micro API compat");
#if !UNITY_2021_2_OR_NEWER
                case ApiCompatibilityLevel.NET_Standard_2_0:
                    return TargetFramework.NetStandard20;
#else
                case ApiCompatibilityLevel.NET_Standard:
                    return TargetFramework.NetStandard21;
#endif
            }

            throw new PlatformNotSupportedException("ApiCompatibilityLevel platform not matched.");
        }

        /// <summary>
        /// Returns the configured <see cref="ScriptingBackend"/> for the <see cref="BuildTargetGroup"/>.
        /// </summary>
        /// <param name="this">The <see cref="BuildTargetGroup"/> to get <see cref="ScriptingBackend"/> for.</param>
        /// <returns>The <see cref="ScriptingBackend"/> configured for given <see cref="BuildTargetGroup"/>.</returns>
        public static ScriptingBackend GetScriptingBackend(this BuildTargetGroup @this)
        {
            if (@this == BuildTargetGroup.Unknown)
            {
                // This may be different on older unity versions
                return ScriptingBackend.Mono;
            }

            switch (PlayerSettings.GetScriptingBackend(@this))
            {
                case ScriptingImplementation.Mono2x:
                    return ScriptingBackend.Mono;
                case ScriptingImplementation.IL2CPP:
                    return ScriptingBackend.IL2CPP;
                case ScriptingImplementation.WinRTDotNET:
                    return ScriptingBackend.Net;
            }

            throw new PlatformNotSupportedException("ScriptingBackend platform not matched.");
        }
    }
}
#endif