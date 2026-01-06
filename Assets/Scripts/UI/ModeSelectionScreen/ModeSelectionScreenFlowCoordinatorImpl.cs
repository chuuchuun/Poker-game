using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelectionScreenFlowCoordinatorImpl : ModeSelectionScreenFlowCoordinator
{
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        Debug.Log("Starting Host...");
        GameManager.Instance.IsSingleplayer = true;
        GameManager.Instance.StartHost();
    }

    public void MultiplayerMode() {
        Debug.Log("Multiplayer mode start");
        SceneManager.LoadScene("MultiplayerScreen");
    }

    public void SingleplayerMode()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("MainScene");
    }

    public void BackToMainMenu()
    {
        Debug.Log("Back to main menu");
        SceneManager.LoadScene("MainMenu");
    }
}