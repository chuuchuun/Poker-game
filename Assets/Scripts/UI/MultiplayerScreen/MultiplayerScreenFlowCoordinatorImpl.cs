using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerScreenFlowCoordinatorImpl : MultiplayerScreenFlowCoordinator
{
    private bool isCreatingGame = false;
    private string selectedLobbyIP = "";

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

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager is not accessible.");
            return;
        }

        if (scene.name == "MainScene")
        {
            if (isCreatingGame)
            {
                if (!NetworkManager.Singleton.IsHost)
                {
                    Debug.Log("Starting Host...");
                    GameManager.Instance.StartHost();
                    LANLobbyManager.Instance.StartHostLAN();
                }
            }
            else
            {
                if (!NetworkManager.Singleton.IsClient)
                {
                    if (!string.IsNullOrEmpty(selectedLobbyIP))
                    {
                        Debug.Log($"Joining LAN Game at {selectedLobbyIP}...");
                        LANLobbyManager.Instance.JoinGameLAN(selectedLobbyIP);
                    }
                    else
                    {
                        Debug.Log("Joining default GameManager flow...");
                        GameManager.Instance.JoinGame();
                    }
                }
            }
        }
    }

    public void CreateGame(string lobbyName = "My LAN Lobby")
    {
        Debug.Log("CreateGame method called.");

        isCreatingGame = true;

        LANLobbyManager.Instance.LobbyName = lobbyName;

        SceneManager.LoadScene("MainScene");
    }

    public void JoinGame(string lobbyIP)
    {
        Debug.Log($"JoinGame (LAN) method called. Lobby IP: {lobbyIP}");

        isCreatingGame = false;
        selectedLobbyIP = lobbyIP;

        SceneManager.LoadScene("MainScene");
    }
    public void JoinGame()
    {
        Debug.Log("JoinGame method called (no IP).");

        isCreatingGame = false;
        selectedLobbyIP = "";

        SceneManager.LoadScene("MainScene");
    }

    public void BackToModeSelection()
    {
        Debug.Log("Back to mode selection method called.");
        SceneManager.LoadScene("ModeSelectionScene");
    }
    public List<LobbyInfo> GetAvailableLobbies()
    {
        LANLobbyManager.Instance.StartListeningForLobbies();
        return LANLobbyManager.Instance.AvailableLobbies;
    }
}
