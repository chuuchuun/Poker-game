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
        if (IsLocalPlayer)
        {
            playerCamera.gameObject.SetActive(true);
        }
        else
        {
            playerCamera.gameObject.SetActive(false);
        }
    }
}
