using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    public Transform cameraPosition;

    private void Awake()
    {
        // Check if the current object has a parent
        if (transform.parent != null)
        {
            // Get all child Transforms of the parent object
            Transform[] siblingsAndDescendants = transform.parent.GetComponentsInChildren<Transform>(true);

            // Search for the "Orientation" Transform
            foreach (Transform t in siblingsAndDescendants)
            {
                if (t.name == "CameraPosition")
                {
                    cameraPosition = t;
                    break; // Stop searching once the target is found
                }
            }

            if (cameraPosition == null)
            {
                Debug.LogError("CameraPosition Transform not found in parent's children!");
            }
        }
        else
        {
            Debug.LogError("This object does not have a parent!");
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = cameraPosition.position;
    }
}
