using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UIGameController : MonoBehaviour
{
    ControlHintViewController controlHintViewController;
    private void Start()
    {
        controlHintViewController = FindObjectsOfType<ControlHintViewController>().First();
        Debug.Log($"Set, {controlHintViewController}");
    }

    public void ToggleControlHintVisibility()
    {
        bool state = controlHintViewController.IsControlHistVisible();
        SetControlHintVisibility(!state);
    }

    public void SetControlHintVisibility(bool state)
    {
        controlHintViewController.SetVisibility(state);
    }
}