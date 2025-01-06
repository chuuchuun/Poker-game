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
        FlowCoordinator.ExitGame();
    }
}
