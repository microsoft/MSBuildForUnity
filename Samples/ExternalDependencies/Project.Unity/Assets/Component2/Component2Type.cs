#if UNITY_EDITOR
using Microsoft.Build.Unity.ProjectGeneration;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Component2Type : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
#if UNITY_EDIITOR
        MSBuildTools.GenerateSDKProjects();
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
