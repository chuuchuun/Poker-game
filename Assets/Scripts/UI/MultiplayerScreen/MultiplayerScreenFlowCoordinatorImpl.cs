using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerScreenFlowCoordinatorImpl : MultiplayerScreenFlowCoordinator
{
    private bool isCreatingGame = false;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene loaded: " + scene.name);

        if (GameManager.Instance != null)
        {
            Debug.Log("GameManager is available");

            if (scene.name == "MainScene")
            {
                if (isCreatingGame)
                {
                    if (!NetworkManager.Singleton.IsHost)
                    {
                        Debug.Log("Starting Host...");
                        GameManager.Instance.StartHost();
                    }
                }
                else
                {
                    if (!NetworkManager.Singleton.IsClient)
                    {
                        Debug.Log("Joining Game...");
                        GameManager.Instance.JoinGame();
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("GameManager is not accessible.");
        }
    }

    public void CreateGame()
    {
        Debug.Log("CreateGame method called.");

        isCreatingGame = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene("MainScene");
    }

    public void JoinGame()
    {
        Debug.Log("JoinGame method called.");

        isCreatingGame = false;
        SceneManager.sceneLoaded += OnSceneLoaded;

        SceneManager.LoadScene("MainScene");
    }

    public void BackToModeSelection()
    {
        Debug.Log("Back to mode selection method called.");
        SceneManager.LoadScene("ModeSelectionScene");
    }
}
