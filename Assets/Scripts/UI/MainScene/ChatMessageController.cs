using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ChatMessageController : NetworkBehaviour
{
    [Header("Chat UI References")]
    [SerializeField] private Canvas chatCanvas;
    [SerializeField] private TMP_Text chatTextPrefab;
    [SerializeField] private Transform chatContentParent;
    [SerializeField] private int maxMessages = 20;
    [SerializeField] private float messageLifetime = 10f;

    [Header("Visual Settings")]
    [SerializeField] private Color raiseColor = Color.yellow;
    [SerializeField] private Color foldColor = Color.red;
    [SerializeField] private Color checkColor = Color.green;
    [SerializeField] private Color callColor = Color.blue;
    [SerializeField] private Color reRaiseColor = Color.magenta;
    [SerializeField] private Color systemColor = Color.white;
    [SerializeField] private Color joinColor = Color.cyan;
    [SerializeField] private Color winColor = Color.yellow;

    [Header("Timestamps")]
    [SerializeField] private bool showTimestamps = true;
    [SerializeField] private string timestampFormat = "HH:mm:ss";

    private Queue<GameObject> messageQueue = new Queue<GameObject>();
    private Dictionary<BetAction, Color> actionColors;

    public override void OnNetworkSpawn()
    {
        InitializeColors();
        InitializeChat();

        if (chatCanvas != null)
        {
            chatCanvas.enabled = true;
        }

        Debug.Log($"Chat system spawned for client {NetworkManager.Singleton.LocalClientId}");
    }

    private void InitializeColors()
    {
        actionColors = new Dictionary<BetAction, Color>
        {
            { BetAction.fold, foldColor },
            { BetAction.check, checkColor },
            { BetAction.call, callColor },
            { BetAction.raise, raiseColor },
            { BetAction.reRaise, reRaiseColor },
            { BetAction.start, systemColor }
        };
    }

    private void InitializeChat()
    {
        if (chatCanvas == null)
            chatCanvas = GetComponent<Canvas>();

        if (chatCanvas != null)
        {
            chatCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        foreach (Transform child in chatContentParent)
        {
            Destroy(child.gameObject);
        }
    }



    #region Public API for Other Classes

    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerActionServerRpc(ulong playerId, BetAction action, int amount = 0)
    {
        if (!IsServer) return;

        string message = FormatPlayerActionMessage(playerId, action, amount);
        Color color = GetActionColor(action);

        ReceiveMessageClientRpc(playerId, (int)action, amount, message, ColorUtility.ToHtmlStringRGB(color));
        Debug.Log($"[Chat] {message}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendGameMessageServerRpc(string message)
    {
        if (!IsServer) return;

        ReceiveGameMessageClientRpc(message);
        Debug.Log($"[Chat] Game: {message}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerJoinedServerRpc(ulong playerId)
    {
        if (!IsServer) return;

        string message = $"Player {playerId} joined the game";
        ReceiveGameMessageClientRpc(message);
        Debug.Log($"[Chat] {message}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendPlayerLeftServerRpc(ulong playerId)
    {
        if (!IsServer) return;

        string message = $"Player {playerId} left the game";
        ReceiveGameMessageClientRpc(message);
        Debug.Log($"[Chat] {message}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendRoundStartServerRpc(ulong firstPlayerId)
    {
        if (!IsServer) return;

        string message = $"Round started! First player: Player {firstPlayerId}";
        ReceiveGameMessageClientRpc(message);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendRoundEndServerRpc()
    {
        if (!IsServer) return;

        string message = "Round ended - determining winner...";
        ReceiveGameMessageClientRpc(message);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendWinnerMessageServerRpc(ulong winnerId, int potAmount)
    {
        if (!IsServer) return;

        string message = $"Player {winnerId} wins ${potAmount}!";
        ReceiveGameMessageClientRpc(message, ColorUtility.ToHtmlStringRGB(winColor));
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendSplitPotServerRpc(ulong[] winnerIds, int potAmount)
    {
        if (!IsServer) return;

        string winnersText = string.Join(", ", winnerIds.Select(w => $"Player {w}"));
        string message = $"Split pot! Winners: {winnersText} - ${potAmount} each";
        ReceiveGameMessageClientRpc(message, ColorUtility.ToHtmlStringRGB(winColor));
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendTurnChangeServerRpc(ulong playerId)
    {
        if (!IsServer) return;

        string message = $"Player {playerId}'s turn";
        ReceiveGameMessageClientRpc(message);
    }

    #endregion

    #region Client RPC Handlers

    [ClientRpc]
    private void ReceiveMessageClientRpc(ulong playerId, int actionType, int amount, string message, string colorHex)
    {
        Color color;
        if (ColorUtility.TryParseHtmlString($"#{colorHex}", out color))
        {
            AddMessage(FormatWithTimestamp(message), color);
        }
        else
        {
            AddMessage(FormatWithTimestamp(message), systemColor);
        }
    }

    [ClientRpc]
    private void ReceiveGameMessageClientRpc(string message, string colorHex = null)
    {
        Color color = systemColor;
        if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString($"#{colorHex}", out Color parsedColor))
        {
            color = parsedColor;
        }

        AddMessage(FormatWithTimestamp($"[System] {message}"), color);
    }

    #endregion

    #region Message Formatting

    private string FormatPlayerActionMessage(ulong playerId, BetAction action, int amount)
    {
        string playerName = $"Player {playerId}";

        return action switch
        {
            BetAction.fold => $"{playerName} folded",
            BetAction.check => $"{playerName} checked",
            BetAction.call => $"{playerName} called ${amount}",
            BetAction.raise => $"{playerName} raised to ${amount}",
            BetAction.reRaise => $"{playerName} re-raised to ${amount}",
            BetAction.start => $"{playerName} started the game",
            _ => $"{playerName} performed {action}"
        };
    }

    private string FormatWithTimestamp(string message)
    {
        if (showTimestamps)
        {
            return $"[{System.DateTime.Now.ToString(timestampFormat)}] {message}";
        }
        return message;
    }

    private Color GetActionColor(BetAction action)
    {
        return actionColors.ContainsKey(action) ? actionColors[action] : systemColor;
    }

    #endregion

    #region Internal Message Management

    private void AddMessage(string message, Color color)
    {
        if (chatTextPrefab == null || chatContentParent == null) return;

        TMP_Text newMessage = Instantiate(chatTextPrefab, chatContentParent);
        newMessage.text = message;
        newMessage.color = color;

        messageQueue.Enqueue(newMessage.gameObject);

        if (messageQueue.Count > maxMessages)
        {
            GameObject oldestMessage = messageQueue.Dequeue();
            Destroy(oldestMessage);
        }

        StartCoroutine(AutoRemoveMessage(newMessage.gameObject));
    }

    private System.Collections.IEnumerator AutoRemoveMessage(GameObject messageObject)
    {
        yield return new WaitForSeconds(messageLifetime);

        if (messageObject != null && messageQueue.Contains(messageObject))
        {
            var newQueue = new Queue<GameObject>();
            foreach (var msg in messageQueue)
            {
                if (msg != messageObject)
                    newQueue.Enqueue(msg);
            }
            messageQueue = newQueue;
            Destroy(messageObject);
        }
    }

    #endregion

    #region Utility Methods

    public void ClearChat()
    {
        foreach (var message in messageQueue)
        {
            Destroy(message);
        }
        messageQueue.Clear();
    }

    public void ToggleChatVisibility()
    {
        if (chatCanvas != null)
        {
            chatCanvas.enabled = !chatCanvas.enabled;
        }
    }

    public void QuickPlayerAction(ulong playerId, BetAction action, int amount = 0)
    {
        if (IsServer)
        {
            SendPlayerActionServerRpc(playerId, action, amount);
        }
    }

    public void QuickGameMessage(string message)
    {
        if (IsServer)
        {
            SendGameMessageServerRpc(message);
        }
    }

    #endregion
}