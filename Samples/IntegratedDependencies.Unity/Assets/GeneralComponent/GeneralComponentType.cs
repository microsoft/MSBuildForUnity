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
        return "IntegartedDependencies Project + {commonComponent.GetData()}";
    }
}
