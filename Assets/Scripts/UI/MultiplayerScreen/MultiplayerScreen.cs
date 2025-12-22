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
            ShowCreateLobbyPopup();
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

    private void ShowCreateLobbyPopup()
    {
        if (PopUpManager.Instance == null)
        {
            Debug.LogWarning("PopUpManager instance not found. Falling back to default lobby name.");
            FlowCoordinator.CreateGame("Test");
            return;
        }

        PopUpManager.OnTextSubmitted += OnLobbyNameSubmitted;
        PopUpManager.Instance.OpenPopup(PopupMode.Text, "Enter lobby name", "", "Start Game");
    }

    private void OnLobbyNameSubmitted(string lobbyName)
    {
        PopUpManager.OnTextSubmitted -= OnLobbyNameSubmitted;

        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            Debug.LogWarning("Lobby name was empty, using fallback name 'Test'");
            lobbyName = "Test";
        }

        FlowCoordinator.CreateGame(lobbyName);
    }

    public void CreateGame()
    {
        ShowCreateLobbyPopup();
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
