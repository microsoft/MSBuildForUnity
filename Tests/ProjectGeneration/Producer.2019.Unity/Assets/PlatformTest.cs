// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        public TestResult RunTest()
        {
#if UNITY_ANDROID

#elif UNITY_IPHONE

#elif UNITY_WSA

#elif UNITY_STANDALONE

#endif

            return TestResult.PlatformNotTested;
        }
    }
}