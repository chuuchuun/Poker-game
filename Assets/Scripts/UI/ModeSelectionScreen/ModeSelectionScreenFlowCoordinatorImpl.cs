using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelectionScreenFlowCoordinatorImpl : ModeSelectionScreenFlowCoordinator
{
    public void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
<<<<<<< HEAD
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("MainScene");
=======
        SceneManager.LoadScene("DifficultyScreen");
        Debug.Log("Difficulty screen show");
>>>>>>> 3daa40a (Implemented preloader screen and modified all ui)
    }

    public void BackToMainMenu()
    {
        Debug.Log("Back to main menu");
<<<<<<< HEAD
=======
        SceneManager.LoadScene("MainMenu");
>>>>>>> 3daa40a (Implemented preloader screen and modified all ui)
    }
}