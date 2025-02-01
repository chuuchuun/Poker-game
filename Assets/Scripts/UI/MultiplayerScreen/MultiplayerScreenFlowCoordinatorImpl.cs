using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerScreenFlowCoordinatorImpl : MultiplayerScreenFlowCoordinator
{
    private bool isCreatingGame = false;  // Flag to track if we're creating a game or joining

    private void OnEnable()
    {
        // Subscribe to the sceneLoaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from the sceneLoaded event
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
                    // Start host if creating a game
                    if (!NetworkManager.Singleton.IsHost)
                    {
                        Debug.Log("Starting Host...");
                        GameManager.Instance.StartHost();
                    }
                }
                else
                {
                    // Join game if joining
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

        // Set flag to indicate we're creating a game
        isCreatingGame = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Load the main scene and trigger the host logic in OnSceneLoaded
        SceneManager.LoadScene("MainScene");
    }

    public void JoinGame()
    {
        Debug.Log("JoinGame method called.");

        // Set flag to indicate we're joining a game
        isCreatingGame = false;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Load the main scene and trigger the client logic in OnSceneLoaded
        SceneManager.LoadScene("MainScene");
    }

    public void BackToModeSelection()
    {
        Debug.Log("Back to mode selection method called.");
        SceneManager.LoadScene("ModeSelectionScene");
    }
}
