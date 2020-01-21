// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using CommonLibrary;
using UnityEngine;

public class GeneralComponentType : MonoBehaviour
{
    private CommonComponent commonComponent = new CommonComponent();
    private void Start()
    {
        Debug.Log(commonComponent.GetData());
    }

    public string GetThisDataToo()
    {
        return $"IntegartedDependencies Project + {commonComponent.GetData()}";
    }
}
