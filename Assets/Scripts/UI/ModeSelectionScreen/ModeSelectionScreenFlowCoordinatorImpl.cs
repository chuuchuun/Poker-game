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
        SceneManager.sceneLoaded += OnSceneLoaded;
<<<<<<< HEAD
        SceneManager.LoadScene("MainScene");       
=======
        SceneManager.LoadScene("MainScene");
>>>>>>> a868526 (fixed conflicts)
    }

    public void BackToMainMenu()
    {
<<<<<<< HEAD
        Debug.Log("Back to main menu");
=======
>>>>>>> a868526 (fixed conflicts)
        SceneManager.LoadScene("MainMenu");
    }
}