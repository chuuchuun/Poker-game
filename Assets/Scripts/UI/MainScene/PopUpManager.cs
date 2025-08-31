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
        if (int.TryParse(input, out int betAmount) && betAmount > 0)
        {
            Debug.Log($"Bet amount submitted: {betAmount}");
            OnBetAmountSubmitted?.Invoke(betAmount);
            ClosePopup();
        }
        else
        {
            Debug.LogWarning($"Invalid input: {input}");
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