using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuFlowCoordinatorImpl: MainMenuFlowCoordinator {
    public void ShowPlayScreen() {
        SceneManager.LoadScene("ModeSelectionScreen");
        Debug.Log("Play screen show");
    }

    public void ShowSettingsScreen() {
        Debug.Log("Settings screen show");
    }

    public void ExitGame() {
        Application.Quit();
    }
}