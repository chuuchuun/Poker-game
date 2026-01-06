using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Linq;

public class SettingsMenuController : MonoBehaviour
{
    public Canvas mainCanvas;
    public Slider volumeSlider;
    public float defaultVolume = 0.8f;
    public Toggle hintsToggle;
    public Button exitButton;
    public static SettingsMenuController Instance { get; private set; }
    private PlayerInput playerInput;
    public static event Action<bool> OnMenuStateChanged;

    private void Awake()
    {
        Instance = this;

        playerInput = FindObjectOfType<PlayerInput>();
        if (playerInput == null)
            Debug.LogWarning("PlayerInput not found in scene!");

        mainCanvas.gameObject.SetActive(false);

        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", defaultVolume);
        AudioListener.volume = savedVolume;
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped += OnServerStopped;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        }
    }

    private void OnServerStopped(bool _)
    {
        ExitLobby();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ExitLobby();
        }
    }

    private void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (hintsToggle != null)
        {
            hintsToggle.isOn = PlayerPrefs.GetInt("HintsEnabled", 1) == 1;
            hintsToggle.onValueChanged.AddListener(SetHints);
        }

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitLobby);
    }

    public void SetVisibility(bool state)
    {
        mainCanvas.gameObject.SetActive(state);

        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;

        if (playerInput != null)
            playerInput.enabled = !state;

        OnMenuStateChanged?.Invoke(state);
    }

    public void ToggleMenu()
    {
        bool showMenu = !mainCanvas.gameObject.activeSelf;
        SetVisibility(showMenu);
    }

    public void SetHints(bool enabled)
    {
        PlayerPrefs.SetInt("HintsEnabled", enabled ? 1 : 0);
        PlayerPrefs.Save();

        var ui = FindObjectOfType<UIGameController>();
        if (ui != null)
            ui.SetControlHintVisibility(enabled);
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;

        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
    }

    public void ExitLobby()
    {
        var localPlayer = FindObjectsOfType<MonoBehaviour>().OfType<IPlayerController>().FirstOrDefault(p => p.IsOwner);
        if (localPlayer != null)
        {
            localPlayer.Act(BetAction.fold);
            Debug.Log("Local player folded before leaving the lobby.");

            StartCoroutine(DisconnectAndLoadScene());
        }
        else
        {
            if (GameManager.Instance.IsSingleplayer) {
                SceneManager.LoadScene("ModeSelectionScreen");
            } else {
                SceneManager.LoadScene("MultiplayerScreen");
            }
        }
    }

    private IEnumerator DisconnectAndLoadScene()
    {
        ulong clientId = NetworkManager.Singleton.LocalClientId;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.DisconnectClient(clientId);
        }

        yield return null;

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.Shutdown();
                LANLobbyManager.Instance.StopBroadcasting();
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        if (GameManager.Instance.IsSingleplayer)
        {
            SceneManager.LoadScene("ModeSelectionScreen");
        }
        else
        {
            SceneManager.LoadScene("MultiplayerScreen");
        }
    }
}
