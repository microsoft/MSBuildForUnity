// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

public class CrossUnityTestScript : MonoBehaviour
{
    private GeneralComponentType generalComponentType = new GeneralComponentType();

    private void Start()
    {
        Debug.Log(generalComponentType.GetThisDataToo());
    }
}
