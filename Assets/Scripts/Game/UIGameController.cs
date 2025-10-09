using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UIGameController : MonoBehaviour
{
    ControlHintViewController controlHintViewController;
    public SettingsMenuController settingsMenuController;
    public static UIGameController Instance;
    private void Awake() => Instance = this;

    private void Start()
    {
        controlHintViewController = FindObjectsOfType<ControlHintViewController>().First();
        Debug.Log($"Set, {controlHintViewController}");
        if(settingsMenuController != null)
            Debug.Log($"Set, {settingsMenuController}");
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

    public void ToggleSettingsMenuVisibility()
    {
        settingsMenuController.ToggleMenu();
    }
}