using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ChatManager : NetworkBehaviour
{
    public static ChatManager Instance { get; private set; }

    [Header("Chat UI References")]
    [SerializeField] private Canvas chatCanvas;
    [SerializeField] private TMP_Text chatTextPrefab;
    [SerializeField] private Transform chatContentParent;
    [SerializeField] private int maxMessages = 20;
    [SerializeField] private float messageLifetime = 10f;

    [Header("Screen Size Settings")]
    [SerializeField] private float widthPercentage = 0.25f;
    [SerializeField] private float heightPercentage = 0.3f;
    [SerializeField] private float rightMargin = 20f;
    [SerializeField] private float bottomMargin = 20f;
    [SerializeField] private float topMargin = 40f;

    [Header("Font Settings")]
    [SerializeField] private int baseFontSize = 14;
    [SerializeField] private float fontSizeMultiplier = 1.0f;
    [SerializeField] private bool autoAdjustFontSize = true;
    [SerializeField] private int minFontSize = 10;
    [SerializeField] private int maxFontSize = 20;

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
    private RectTransform canvasRect;
    private RectTransform contentParentRect;
    private VerticalLayoutGroup layoutGroup;
    private int currentFontSize;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeColors();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[ChatManager] OnNetworkSpawn | IsServer: {IsServer} | IsClient: {IsClient}");

        InitializeChat();

        if (IsServer)
        {
            StartCoroutine(SendWelcomeMessageAfterDelay());
        }
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
            chatCanvas.enabled = true;

            canvasRect = chatCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                SetupCanvasSizeAndPosition();
            }
        }

        CalculateOptimalFontSize();

        if (chatContentParent != null)
        {
            contentParentRect = chatContentParent.GetComponent<RectTransform>();

            layoutGroup = chatContentParent.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = chatContentParent.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layoutGroup.padding = new RectOffset(10, 10, (int)topMargin, 10);
            layoutGroup.spacing = 5f;
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = false;

            foreach (Transform child in chatContentParent)
            {
                Destroy(child.gameObject);
            }
        }

        messageQueue.Clear();

        InvokeRepeating("CheckScreenSize", 1f, 2f);
    }

    private void SetupCanvasSizeAndPosition()
    {
        if (canvasRect == null) return;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        float chatWidth = screenWidth * widthPercentage;
        float chatHeight = screenHeight * heightPercentage;

        canvasRect.anchorMin = new Vector2(1f, 0f);
        canvasRect.anchorMax = new Vector2(1f, 0f);
        canvasRect.pivot = new Vector2(1f, 0f);

        canvasRect.sizeDelta = new Vector2(chatWidth, chatHeight);

        canvasRect.anchoredPosition = new Vector2(-rightMargin, bottomMargin);

        if (autoAdjustFontSize)
        {
            CalculateOptimalFontSize();
        }

        Debug.Log($"Chat positioned at bottom-right. Size: {chatWidth}x{chatHeight}, Font Size: {currentFontSize}");
    }

    private void CalculateOptimalFontSize()
    {
        if (!autoAdjustFontSize)
        {
            currentFontSize = baseFontSize;
            return;
        }

        float screenHeight = Screen.height;
        float chatHeight = screenHeight * heightPercentage;

        float sizeMultiplier = chatHeight / 500f;

        currentFontSize = Mathf.RoundToInt(baseFontSize * sizeMultiplier * fontSizeMultiplier);
        currentFontSize = Mathf.Clamp(currentFontSize, minFontSize, maxFontSize);

        Debug.Log($"Calculated font size: {currentFontSize} (Screen: {screenHeight}, Chat: {chatHeight})");
    }

    private void CheckScreenSize()
    {
        SetupCanvasSizeAndPosition();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SetupCanvasSizeAndPosition();
        }
    }

    private System.Collections.IEnumerator SendWelcomeMessageAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        SendGameMessageServerRpc("Welcome to Poker Game!");
    }

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

    [ClientRpc]
    private void ReceiveMessageClientRpc(ulong playerId, int actionType, int amount, string message, string colorHex)
    {
        Debug.Log($"[Chat ClientRpc] Received player action: {message}");

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
        Debug.Log($"[Chat ClientRpc] Received game message: {message}");

        Color color = systemColor;
        if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString($"#{colorHex}", out Color parsedColor))
        {
            color = parsedColor;
        }

        AddMessage(FormatWithTimestamp($"[System] {message}"), color);
    }

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

    private void AddMessage(string message, Color color)
    {
        if (chatTextPrefab == null || chatContentParent == null)
        {
            Debug.LogWarning("[Chat] Cannot add message - missing prefab or parent");
            return;
        }

        TMP_Text newMessage = Instantiate(chatTextPrefab, chatContentParent);
        newMessage.text = message;
        newMessage.color = color;

        newMessage.fontSize = currentFontSize;

        newMessage.enableAutoSizing = true;
        newMessage.fontSizeMin = minFontSize;
        newMessage.fontSizeMax = maxFontSize;

        LayoutElement layoutElement = newMessage.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = newMessage.gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.preferredHeight = newMessage.preferredHeight;

        Debug.Log($"[Chat] Displayed message: {message} (Font: {currentFontSize}px)");

        messageQueue.Enqueue(newMessage.gameObject);

        if (messageQueue.Count > maxMessages)
        {
            GameObject oldestMessage = messageQueue.Dequeue();
            Destroy(oldestMessage);
        }

        StartCoroutine(ScrollToBottom());
        StartCoroutine(AutoRemoveMessage(newMessage.gameObject));
    }

    private System.Collections.IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        if (contentParentRect != null)
        {
            Canvas.ForceUpdateCanvases();
            contentParentRect.anchoredPosition = new Vector2(
                contentParentRect.anchoredPosition.x,
                0f
            );
        }
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

    public void SetChatSize(float widthPercent, float heightPercent)
    {
        widthPercentage = Mathf.Clamp(widthPercent, 0.1f, 0.8f);
        heightPercentage = Mathf.Clamp(heightPercent, 0.1f, 0.8f);
        SetupCanvasSizeAndPosition();
    }

    public void SetChatPosition(float rightMarginPixels, float bottomMarginPixels)
    {
        rightMargin = rightMarginPixels;
        bottomMargin = bottomMarginPixels;
        SetupCanvasSizeAndPosition();
    }

    public void SetTopMargin(float newMargin)
    {
        topMargin = newMargin;
        if (layoutGroup != null)
        {
            layoutGroup.padding = new RectOffset(10, 10, (int)newMargin, 10);
        }
    }

    public void SetFontSize(int newBaseSize)
    {
        baseFontSize = Mathf.Clamp(newBaseSize, 8, 30);
        CalculateOptimalFontSize();
        UpdateExistingMessagesFontSize();
    }

    public void SetFontSizeMultiplier(float multiplier)
    {
        fontSizeMultiplier = Mathf.Clamp(multiplier, 0.5f, 3.0f);
        CalculateOptimalFontSize();
        UpdateExistingMessagesFontSize();
    }

    public void SetAutoFontSize(bool enable)
    {
        autoAdjustFontSize = enable;
        CalculateOptimalFontSize();
        UpdateExistingMessagesFontSize();
    }

    private void UpdateExistingMessagesFontSize()
    {
        foreach (var messageObj in messageQueue)
        {
            TMP_Text textComponent = messageObj.GetComponent<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.fontSize = currentFontSize;
            }
        }
    }

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
}