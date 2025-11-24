using UnityEngine;
using Unity.Netcode;

public class PlayerCameraManager : NetworkBehaviour
{
    public Camera playerCamera;
    private AudioListener audioListener;

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        audioListener = GetComponentInParent<AudioListener>();

        // Ensure AudioListener exists
        if (audioListener == null && playerCamera != null)
        {
            audioListener = playerCamera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void Start()
    {
        if (IsOwner)
        {
            EnableLocalCamera();
        }
        else
        {
            DisableCamera();
        }
    }

    private void EnableLocalCamera()
    {
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(true);
            playerCamera.enabled = true;
        }

        if (audioListener != null)
        {
            audioListener.enabled = true;
        }
    }

    private void DisableCamera()
    {
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(false);
            playerCamera.enabled = false;
        }

        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
    }

    public override void OnDestroy()
    {
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
        base.OnDestroy();
    }
}