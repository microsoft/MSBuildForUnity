// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using CommonLibrary;
using UnityEngine;

public class NonComponentType : MonoBehaviour
{
    private void Start()
    {
        Debug.Log(new CommonComponent().GetData());
    }
}
