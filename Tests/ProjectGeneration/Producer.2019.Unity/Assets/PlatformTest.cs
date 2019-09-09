// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Linq;
using UnityEngine;

namespace Microsoft.Build.Unity.ProjectGeneration.Test
{
    [Serializable]
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

            bool isEditor = false;
#if UNITY_EDITOR
            isEditor = true;
            Debug.Log($"Found '{UnityEditor.AssetDatabase.GetAllAssetPaths().Length}' assets in the consuming project");
            if (!UnityEditor.AssetDatabase.GetAllAssetPaths().Any(t => t.Contains("Microsoft.Build.Unity.ProjectGeneration.TestAssembly")))
            {
                Debug.LogError("Couldn't find this assembly (Microsoft.Build.Unity.ProjectGeneration.TestAssembly) as an asset of the consuming project.");
                return TestResult.Failure;
            }
            testedEditor = true;
#endif

            if (!isEditor)
            {
                testedEditor = true;
            }

#if UNITY_ANDROID
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityPlayer == null)
            {
                Debug.LogError("Failed to get the unity player class.");
            }

            if (isEditor)
            {
                Debug.Log($"We are in editor, compling android, but won't have activity.");
            }
            else if (unityActivity == null)
            {
                Debug.LogError($"Failed to get Unity Activity.");
                return TestResult.Failure;
            }

            testedPlatform = true;
#elif UNITY_IPHONE
            string id = UnityEngine.iOS.Device.advertisingIdentifier;
            Debug.Log($"Our iOS Advertising Id is '{id}'");

            testedPlatform = true;
#elif UNITY_WSA
            using (UnityEngine.XR.WSA.Input.GestureRecognizer gestureRecognizer = new UnityEngine.XR.WSA.Input.GestureRecognizer())
            {
                Debug.Log($"We can recognize the following gestures '{gestureRecognizer.GetRecognizableGestures()}'");
            }
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
#if !UNITY_EDITOR
                    Debug.Log("About to try creating a file");
                    Windows.Storage.StorageFolder storageFolder = Windows.Storage.KnownFolders.PicturesLibrary;
                    Windows.Storage.StorageFile file = await storageFolder.CreateFileAsync("sample.png", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                    Debug.Log($"Created sample.png, now will delete");
                    await file.DeleteAsync();
#endif
                    await System.Threading.Tasks.Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Encountered an error when trying to create/delete file: {ex.ToString()}");
                }
            }).Wait();

            testedPlatform = true;
#elif UNITY_STANDALONE
            System.IntPtr handle = GetActiveWindow();
            if (handle == System.IntPtr.Zero)
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

            return (testedEditor && testedPlatform) ? TestResult.Success : TestResult.PlatformNotTested;
        }
    }
}