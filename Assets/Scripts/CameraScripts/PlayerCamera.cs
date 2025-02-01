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
    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Awake()
    {
        if (transform.parent != null)
        {
            Transform[] siblingsAndDescendants = transform.parent.parent.GetComponentsInChildren<Transform>(true);

            // Search for the "Orientation" Transform
            foreach (Transform t in siblingsAndDescendants)
            {
                if (t.name == "Orientation")
                {
                    orientation = t;
                    break; // Stop searching once the target is found
                }
            }

            if (orientation == null)
            {
                Debug.LogError("Orientation Transform not found in parent's children!");
            }
        }
        else
        {
            Debug.LogError("This object does not have a parent!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime* sensY;

        yRotation += mouseX;
        xRotation -= mouseY;

        // xRotation = Mathf.Clamp(xRotation, -50f, 34f);
        //yRotation = Mathf.Clamp(yRotation, -60f, 60f);

        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.rotation = Quaternion.Euler(0, yRotation, 0);
    }
}
