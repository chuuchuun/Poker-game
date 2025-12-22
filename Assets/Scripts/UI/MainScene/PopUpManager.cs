using UnityEngine;
using TMPro;
using System;
using UnityEngine.InputSystem;

public enum PopupMode
{
    BetAmount,
    Text
}

public class PopUpManager : MonoBehaviour
{
    public static PopUpManager Instance { get; private set; }

    public GameObject popupPanel;
    public TMP_InputField inputField;

    public bool IsOpen { get; private set; }

    public static event Action<bool> OnPopupStateChanged;
    public static event Action<int> OnBetAmountSubmitted;
    public static event Action<string> OnTextSubmitted;

    private PopupMode currentMode = PopupMode.BetAmount;

    void Awake()
    {
        if (Instance is null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        popupPanel.SetActive(false);
        IsOpen = false;

        inputField.onSubmit.AddListener(OnInputSubmit);
    }

    private void OnInputSubmit(string input)
    {
        Debug.Log($"Input submitted: {input}");

        if (currentMode == PopupMode.BetAmount)
        {
            if (int.TryParse(input, out int betAmount) && betAmount > 0)
            {
                Debug.Log($"Bet amount submitted: {betAmount}");
                OnBetAmountSubmitted?.Invoke(betAmount);
                ClosePopup();
            }
            else
            {
                Debug.LogWarning($"Invalid input for bet amount: {input}");
                inputField.text = "";
                inputField.Select();
                inputField.ActivateInputField();
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                Debug.Log($"Text submitted: {input}");
                OnTextSubmitted?.Invoke(input);
                ClosePopup();
            }
            else
            {
                Debug.LogWarning("Invalid input for text popup (empty).");
                inputField.text = "";
                inputField.Select();
                inputField.ActivateInputField();
            }
        }
    }

    public void OpenPopup()
    {
        OpenPopup(PopupMode.BetAmount);
    }

    public void OpenPopup(PopupMode mode, string placeholder = "", string defaultText = "", string buttonText = "Bet")
    {
        Debug.Log("Opening popup");
        currentMode = mode;

        popupPanel.SetActive(true);

        var buttonTitle = popupPanel.transform.Find("SubmitButton").GetComponentInChildren<TMP_Text>();
        buttonTitle.text = buttonText;

        if (!string.IsNullOrEmpty(defaultText))
            inputField.text = defaultText;
        else
            inputField.text = "";

        if (!string.IsNullOrEmpty(placeholder) && inputField.placeholder != null)
        {
            if (inputField.placeholder is TMP_Text tmpPlaceholder)
            {
                tmpPlaceholder.text = placeholder;
            }
        }

        inputField.Select();
        inputField.ActivateInputField();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        IsOpen = true;

        Debug.Log("Raising OnPopupStateChanged event (true)");
        OnPopupStateChanged?.Invoke(true);
    }

    private void Update()
    {
        if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePopup();
        }
    }

    public void ClosePopup()
    {
        popupPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        IsOpen = false;

        OnPopupStateChanged?.Invoke(false);
    }

    public void OnSubmitButtonClicked()
    {
        OnInputSubmit(inputField.text);
    }
}