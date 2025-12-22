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

    [Header("Rotation Limits")]
    [SerializeField] private bool clampPitch = true;
    [SerializeField] private float minPitch = -45f;
    [SerializeField] private float maxPitch = 75f;
    [SerializeField] private bool clampYaw = true;
    [SerializeField] private float minYaw = -60f;
    [SerializeField] private float maxYaw = 60f;

    [Header("Position Bounds (optional)")]
    [SerializeField] private bool usePositionBounds = false;
    [SerializeField] private Vector3 boundsMin = new Vector3(-10, 0, -10);
    [SerializeField] private Vector3 boundsMax = new Vector3(10, 5, 10);
    [SerializeField] private BoxCollider boundsSource = null;

    void Awake()
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

            var parent = transform.parent;
            if (parent != null)
            {
                Vector3 parentEuler = parent.rotation.eulerAngles;
                float parentPitch = NormalizeAngle(parentEuler.x);
                float parentYaw = NormalizeAngle(parentEuler.y);

                maxPitch += parentPitch;
                minPitch += parentPitch;
                maxYaw += parentYaw;
                minYaw += parentYaw;

                xRotation = parentPitch;
                yRotation = parentYaw;

                transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);

                if (orientation != null)
                    orientation.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
            else
            {
                Debug.LogError("This object does not have a parent!");
            }

            if (orientation is null) Debug.LogError("Orientation Transform not found in parent's children!");
        }
        else
        {
            Debug.LogError("This object does not have a parent!");
        }
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        PopUpManager.OnPopupStateChanged += HandleUIStateChanged;
        SettingsMenuController.OnMenuStateChanged += HandleUIStateChanged;

        if (boundsSource != null)
        {
            var b = boundsSource.bounds;
            boundsMin = b.min;
            boundsMax = b.max;
            usePositionBounds = true;
        }
    }

    private void OnDestroy()
    {
        PopUpManager.OnPopupStateChanged -= HandleUIStateChanged;
        SettingsMenuController.OnMenuStateChanged -= HandleUIStateChanged;
    }

    private void HandleUIStateChanged(bool isUIOpen)
    {
        canMoveCamera = !isUIOpen;

        Cursor.lockState = isUIOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isUIOpen;
    }

    void Update()
    {
        if (!canMoveCamera) return;

        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensY;

        yRotation += mouseX;
        xRotation -= mouseY;

        if (clampPitch)
            xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

        if (clampYaw)
            yRotation = Mathf.Clamp(yRotation, minYaw, maxYaw);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        if (orientation != null)
            orientation.rotation = Quaternion.Euler(0, yRotation, 0);

        if (usePositionBounds)
        {
            Transform clampTarget = transform.parent != null ? transform.parent : transform;
            Vector3 pos = clampTarget.position;
            pos.x = Mathf.Clamp(pos.x, boundsMin.x, boundsMax.x);
            pos.y = Mathf.Clamp(pos.y, boundsMin.y, boundsMax.y);
            pos.z = Mathf.Clamp(pos.z, boundsMin.z, boundsMax.z);
            clampTarget.position = pos;
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
