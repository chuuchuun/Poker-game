using UnityEngine;
using TMPro;
using System;
using UnityEngine.InputSystem;

public class PopUpManager : MonoBehaviour
{
    public static PopUpManager Instance { get; private set; }

    public GameObject popupPanel;
    public TMP_InputField inputField;

    public bool IsOpen { get; private set; }

    public static event Action<bool> OnPopupStateChanged;
    public static event Action<int> OnBetAmountSubmitted;

    void Awake()
    {
        // Ensure singleton pattern
        if (Instance == null)
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

        // Add listener for input field submission
        inputField.onSubmit.AddListener(OnInputSubmit);
    }

    private void OnInputSubmit(string input)
    {
        Debug.Log($"Input submitted: {input}");
        if (int.TryParse(input, out int betAmount) && betAmount > 0)
        {
            Debug.Log($"Bet amount submitted: {betAmount}");
            OnBetAmountSubmitted?.Invoke(betAmount);
            ClosePopup();
        }
        else
        {
            Debug.LogWarning($"Invalid input: {input}");
            // Clear invalid input and keep focus
            inputField.text = "";
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    public void OpenPopup()
    {
        Debug.Log("Opening popup");
        popupPanel.SetActive(true);

        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        IsOpen = true;

        // Notify subscribers that popup opened
        Debug.Log("Raising OnPopupStateChanged event (true)");
        OnPopupStateChanged?.Invoke(true);
    }

    private void Update()
    {
        // Allow ESC to close popup without submitting
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

        // Notify subscribers that popup closed
        OnPopupStateChanged?.Invoke(false);
    }

    // For UI button submission
    public void OnSubmitButtonClicked()
    {
        OnInputSubmit(inputField.text);
    }
}