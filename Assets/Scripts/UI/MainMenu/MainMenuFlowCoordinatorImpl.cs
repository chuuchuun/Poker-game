using UnityEngine;

public class MainMenuFlowCoordinatorImpl: MainMenuFlowCoordinator {
    public void ShowPlayScreen() {
        Debug.Log("Play screen show");
    }

    public void ShowSettingsScreen() {
        Debug.Log("Settings screen show");
    }

    public void ExitGame() {
        Debug.Log("Exit game");
    }
}