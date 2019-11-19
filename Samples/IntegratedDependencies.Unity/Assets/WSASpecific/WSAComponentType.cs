// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using CommonLibrary.WSA;
using UnityEngine;

public class WSAComponentType : MonoBehaviour
{
    private void Start()
    {
        Debug.Log(new WSAHelperComponent().GetData());
        Debug.Log(Newtonsoft.Json.JsonConvert.DeserializeObject("{ \"test\" : 1 }"));
    }
}
