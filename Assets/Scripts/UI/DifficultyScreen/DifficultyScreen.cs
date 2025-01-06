using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DifficultyScreen : MonoBehaviour
{
    private DifficultyScreenFlowCoordinator FlowCoordinator;
    DifficultyScreen() {
        FlowCoordinator = new DifficultyScreenFlowCoordinatorImpl();
    }

    DifficultyScreen(DifficultyScreenFlowCoordinator mainMenuFlowCoordinator) {
        FlowCoordinator = mainMenuFlowCoordinator;
    }

    public void EasyMode() {
        FlowCoordinator.EasyMode();
    }

    public void MediumMode() {
        FlowCoordinator.MediumMode();
    }

    public void HardMode()
    {
        FlowCoordinator.HardMode();
    }

    public void Back()
    {
        FlowCoordinator.Back();
    }
}
