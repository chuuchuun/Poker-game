using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyListItemPrefab;

    private MultiplayerScreenFlowCoordinator FlowCoordinator;

    MultiplayerScreen()
    {
        FlowCoordinator = new MultiplayerScreenFlowCoordinatorImpl();
    }

    private void OnEnable()
    {
        RefreshLobbyList();
        transform.Find("BackButton").GetComponent<Button>().onClick.AddListener(() =>
        {
            FlowCoordinator.BackToModeSelection();
        });

        transform.Find("RefreshButton").GetComponent<Button>().onClick.AddListener(() =>
        {
            RefreshLobbyList();
        });

        transform.Find("CreateLobbyButton").GetComponent<Button>().onClick.AddListener(() =>
        {
            FlowCoordinator.CreateGame("Test");
        });
    }
    public void RefreshLobbyList()
    {
         ClearLobbyList();

        List<LobbyInfo> lobbies = FlowCoordinator.GetAvailableLobbies();

        foreach (var lobby in lobbies)
        {
            GameObject item = Instantiate(lobbyListItemPrefab, lobbyListContainer, false);

            item.transform.Find("Text_NameValue").GetComponent<TMP_Text>().text = lobby.LobbyName;
            item.transform.Find("Text_PlayersValue").GetComponent<TMP_Text>().text =
                $"{lobby.CurrentPlayers}/6";

            Button button = item.transform.Find("ConnectButton").GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                JoinGame(lobby.LobbyId);
            });
        }
    }

    private void ClearLobbyList()
    {
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void CreateGame()
    {
        FlowCoordinator.CreateGame("Test");
    }

    private void JoinGame(string lobbyId)
    {
        FlowCoordinator.JoinGame(lobbyId);
    }

    public void BackToModeSelection()
    {
        FlowCoordinator.BackToModeSelection();
    }
}
