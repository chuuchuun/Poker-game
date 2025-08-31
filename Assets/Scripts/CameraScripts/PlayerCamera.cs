using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    float sensX = 400;
    float sensY = 400;

    public Transform orientation;

    float xRotation;
    float yRotation;

    private bool canMoveCamera = true;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PopUpManager.OnPopupStateChanged += HandlePopupStateChanged;
    }

    private void OnDestroy()
    {
        PopUpManager.OnPopupStateChanged -= HandlePopupStateChanged;
    }

    private void HandlePopupStateChanged(bool isPopupOpen)
    {
        canMoveCamera = !isPopupOpen;

        if (isPopupOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Awake()
    {
        if (transform.parent != null)
        {
            Transform[] siblingsAndDescendants = transform.parent.parent.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in siblingsAndDescendants)
            {
                if (t.name == "Orientation")
                {
                    orientation = t;
                    break;
                }
            }
            if (orientation is null) Debug.LogError("Orientation Transform not found in parent's children!");
        }
        else
        {
            Debug.LogError("This object does not have a parent!");
        }
    }

    void Update()
    {
        if (!canMoveCamera) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

        yRotation += mouseX;
        xRotation -= mouseY;

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}