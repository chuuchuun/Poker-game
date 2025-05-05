using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlHintViewController : MonoBehaviour
{
    GameObject callText;
    GameObject checkText;
    GameObject foldText;
    GameObject raiseText;
    GameObject reraiseText;

    void Start()
    {
        callText = GameObject.FindGameObjectWithTag("call_text");
        checkText = GameObject.FindGameObjectWithTag("check_text");
        foldText = GameObject.FindGameObjectWithTag("fold_text");
        raiseText = GameObject.FindGameObjectWithTag("raise_text");
        reraiseText = GameObject.FindGameObjectWithTag("reraise_text");
    }

    public bool IsControlHistVisible()
    {
        return gameObject.activeInHierarchy;
    }

    public void SetVisibility(bool state)
    {
        gameObject.SetActive(state);
    }
}
