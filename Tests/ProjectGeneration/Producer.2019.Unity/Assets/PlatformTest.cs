// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Test
{
    public enum TestResult
    {
        Success,
        Failure,
        PlatformNotTested
    }

    public class PlatformTest
    {
        public string Platform => $"{Player}.{Configuration}";

        private string Configuration =>
#if UNITY_EDITOR
            "InEditor";
#else
            "Player";
#endif

        private string Player =>
#if UNITY_ANDROID
            "Android";
#elif UNITY_IPHONE
            "iOS";
#elif UNITY_WSA
            "WSA";
#elif UNITY_STANDALONE
            "Standalone";
#endif

#if UNITY_STANDALONE
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern System.IntPtr GetActiveWindow();
#endif

        public TestResult RunTest()
        {
            bool testedEditor = false;
            bool testedPlatform = false;

#if UNITY_ANDROID

#elif UNITY_IPHONE

#elif UNITY_WSA

#elif UNITY_STANDALONE
            IntPtr handle = GetActiveWindow();
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("Failed to get active window handle.");
                return TestResult.Failure;
            }
            else
            {
                Debug.Log($"Handle of current active window is: {handle.ToInt64()}.");
            }
            testedPlatform = true;
#endif

#if UNITY_EDITOR
            Debug.Log($"Found '{UnityEditor.AssetDatabase.GetAllAssetPaths().Length}' assets in the consuming project");
            if (!UnityEditor.AssetDatabase.GetAllAssetPaths().Any(t => t.Contains("Microsoft.Build.Unity.ProjectGeneration.TestAssembly")))
            {
                Debug.LogError("Couldn't find this assembly (Microsoft.Build.Unity.ProjectGeneration.TestAssembly) as an asset of the consuming project.");
                return TestResult.Failure;
            }
            testedEditor = true;
#else
            testedEditor = true;
#endif

            return (testedEditor && testedPlatform) ? TestResult.Success : TestResult.PlatformNotTested;
        }
    }
}