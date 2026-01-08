using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BalanceCanvasController : MonoBehaviour
{
    [Header("3D Canvas Settings")]
    [SerializeField] private Canvas balanceCanvas;
    [SerializeField] private TMP_Text balanceText;

    [SerializeField] private MonoBehaviour playerControllerBehaviour;
    private IPlayerController playerController;

    [Header("Visual Settings")]
    [SerializeField] private Vector3 canvasOffset = new Vector3(0, 2f, 0);
    [SerializeField] private float faceCameraSpeed = 5f;
    [SerializeField] private bool alwaysFaceCamera = true;

    private int currentBalance = 0;
    private bool isMouseOver = false;
    private Camera mainCamera;

    private void Awake()
    {
        InitializeReferences();
        ConfigureCanvas();
        ConfigureText();
    }

    private void InitializeReferences()
    {
        mainCamera = Camera.main;
        balanceCanvas ??= GetComponent<Canvas>();
        balanceText ??= GetComponentInChildren<TMP_Text>();
    }

    private void ConfigureCanvas()
    {
        if (balanceCanvas == null) return;

        balanceCanvas.renderMode = RenderMode.WorldSpace;
        balanceCanvas.enabled = false;
    }

    private void ConfigureText()
    {
        if (balanceText == null) return;

        balanceText.fontStyle = FontStyles.Bold;
        balanceText.text = "Chips: 0";

        if (!balanceText.TryGetComponent<Outline>(out _))
        {
            Outline outline = balanceText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, 2f);
        }
    }

    private void Start()
    {
        FindMissingReferences();
        SubscribeToEvents();

        if (playerController != null)
        {
            UpdateBalanceDisplay(playerController.CurrentBalance);
        }
    }

    private void FindMissingReferences()
    {
        if (playerControllerBehaviour != null && playerControllerBehaviour is IPlayerController ic)
        {
            playerController = ic;
        }
        else
        {
            var comps = GetComponentsInParent<MonoBehaviour>(includeInactive: true);
            playerController = comps.OfType<IPlayerController>().FirstOrDefault();
            playerControllerBehaviour = (playerController as MonoBehaviour);
        }

        mainCamera ??= FindObjectOfType<Camera>();
    }

    private void SubscribeToEvents()
    {
        if (playerController != null)
        {
            playerController.OnBalanceChanged += HandleBalanceChanged;
            UpdateCanvasPosition();
        }
    }

    private void Update()
    {
        if (ShouldFaceCamera())
        {
            FaceCamera();
        }
    }

    private bool ShouldFaceCamera()
    {
        return alwaysFaceCamera && balanceCanvas != null && balanceCanvas.enabled && mainCamera != null;
    }

    private void OnDestroy()
    {
        if (playerController != null)
        {
            playerController.OnBalanceChanged -= HandleBalanceChanged;
        }
    }

    private void FaceCamera()
    {
        Vector3 directionToCamera = mainCamera.transform.position - balanceCanvas.transform.position;
        directionToCamera.y = 0;

        Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera);
        balanceCanvas.transform.rotation = Quaternion.Slerp(
            balanceCanvas.transform.rotation,
            targetRotation,
            faceCameraSpeed * Time.deltaTime
        );
    }

    private void UpdateCanvasPosition()
    {
        var mb = playerController as MonoBehaviour ?? playerControllerBehaviour;
        if (mb != null && balanceCanvas != null)
        {
            balanceCanvas.transform.position = mb.transform.position + canvasOffset;
        }
    }

    public void Initialize(IPlayerController controller, Canvas canvas = null, TMP_Text text = null)
    {
        playerController = controller;
        playerControllerBehaviour = controller as MonoBehaviour;
        balanceCanvas = canvas ?? balanceCanvas;
        balanceText = text ?? balanceText;

        if (balanceCanvas != null)
        {
            balanceCanvas.renderMode = RenderMode.WorldSpace;
        }

        SubscribeToEvents();

        if (playerController != null)
        {
            int displayBalance = playerController.CurrentBalance;
            UpdateBalanceDisplay(displayBalance);
            Debug.Log($"Canvas initialized with balance: {displayBalance} for player {playerController.PlayerId}");
        }
    }

    public void HandleBalanceChanged(int newBalance)
    {
        currentBalance = newBalance;
        UpdateBalanceDisplay(newBalance);
        Debug.Log($"Balance changed to: {newBalance} for player {playerController?.PlayerId}");

        if (isMouseOver)
        {
            ShowBalance();
        }
    }

    public void UpdateBalanceDisplay(int balance)
    {
        if (balanceText != null)
        {
            balanceText.text = $"Chips: {balance}";
            Debug.Log($"Display updated to: Chips: {balance}");
        }
        else
        {
            Debug.LogError("BalanceText is null!");
        }
    }

    public void ShowBalance()
    {
        if (balanceCanvas != null && playerController != null)
        {
            UpdateBalanceDisplay(playerController.CurrentBalance);
            UpdateCanvasPosition();
            balanceCanvas.enabled = true;
        }
        isMouseOver = true;
    }

    public void HideBalance()
    {
        if (balanceCanvas != null)
        {
            balanceCanvas.enabled = false;
        }
        isMouseOver = false;
    }

    public void SetCanvasOffset(Vector3 offset)
    {
        canvasOffset = offset;
        UpdateCanvasPosition();
    }

    public void OnMouseEnterPlayer() => ShowBalance();
    public void OnMouseExitPlayer() => HideBalance();

    public void ToggleVisibility()
    {
        if (balanceCanvas != null)
        {
            balanceCanvas.enabled = !balanceCanvas.enabled;
        }
    }
}