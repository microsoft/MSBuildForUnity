using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NonComponentType : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor);
    }
}
