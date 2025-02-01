using UnityEngine;
using Unity.Netcode;

public class PlayerCameraManager : NetworkBehaviour
{
    public Camera playerCamera;

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
    }
    private void Start()
    {
        if (IsLocalPlayer) // Ensure only the local player has an active camera
        {
            playerCamera.gameObject.SetActive(true); // Enable the camera for the local player
        }
        else
        {
            playerCamera.gameObject.SetActive(false); // Disable the camera for remote players
        }
    }
}
