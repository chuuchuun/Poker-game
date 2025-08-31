using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MoveCamera : MonoBehaviour
{
    public Transform cameraPosition;

    private void Awake()
    {
        if (transform.parent != null)
        {
            Transform[] siblingsAndDescendants = transform.parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in siblingsAndDescendants)
            {
                if (t.name == "CameraPosition")
                {
                    cameraPosition = t;
                    break;
                }
            }
            if (cameraPosition is null) Debug.LogError("CameraPosition Transform not found in parent's children!");
        }
        else Debug.LogError("This object does not have a parent!");
    }
    void Start()
    {
        
    }

    void Update()
    {
        transform.position = cameraPosition.position;
    }
}
