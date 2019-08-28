using Microsoft.Build.Unity.ProjectGeneration.Test;
using System;
using TMPro;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI platformText = null;

    [SerializeField]
    private TextMeshProUGUI resultsText = null;

    private void Awake()
    {
        PlatformTest testClass = new PlatformTest();
        platformText.text = testClass.Platform;

        try
        {
            var results = testClass.RunTest();
            if (results == TestResult.Failure)
            {
                resultsText.color = Color.red;
            }
            else if (results == TestResult.PlatformNotTested)
            {
                resultsText.color = Color.yellow;
            }
            resultsText.text = results.ToString();
        }
        catch (Exception ex)
        {
            resultsText.text = $"Test Failed; exception:\r\n {ex.ToString()}";
            resultsText.color = Color.red;
        }
    }
}
