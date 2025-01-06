using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuScreen : MonoBehaviour
{
    private MainMenuFlowCoordinator FlowCoordinator;
    MainMenuScreen() {
        FlowCoordinator = new MainMenuFlowCoordinatorImpl();
    }

    MainMenuScreen(MainMenuFlowCoordinator mainMenuFlowCoordinator) {
        FlowCoordinator = mainMenuFlowCoordinator;
    }

    public void ShowPlayScreen() {
        FlowCoordinator.ShowPlayScreen();
    }

    public void ShowSettingsScreen() {
        FlowCoordinator.ShowSettingsScreen();
    }

    public void ExitGame() {
        PokerAlert alert = CreateExitAlert();
        Canvas canvas = gameObject.GetComponent<Canvas>();
        alert.BuildAlert(canvas: canvas);
    }

    private PokerAlert CreateExitAlert()
    {
        GameObject alertObject = new GameObject("PokerAlertObject");
        PokerAlert pokerAlert = alertObject.AddComponent<PokerAlert>();

        pokerAlert.Initialize(
            primaryAction: () =>
            {
                FlowCoordinator.ExitGame();
            },
            secondaryAction: () =>
            {
                pokerAlert.RemoveAlert();
            },
            title: "Are you sure?",
            message: "Are you sure you want to exit the game?",
            primaryButtonTitle: "Yes",
            secondaryButtonTitle: "No"
        );

        return pokerAlert;
    }
}
