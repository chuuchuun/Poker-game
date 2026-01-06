using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ModeSelectionScreen : MonoBehaviour
{
    private ModeSelectionScreenFlowCoordinator FlowCoordinator;
    ModeSelectionScreen() {
        FlowCoordinator = new ModeSelectionScreenFlowCoordinatorImpl();
    }

    ModeSelectionScreen(ModeSelectionScreenFlowCoordinator modeSelectionFlowCoordinator) {
        FlowCoordinator = modeSelectionFlowCoordinator;
    }

    public void SingleplayerMode() {
        FlowCoordinator.SingleplayerMode();
    }

    public void MuliplayerMode() {
        FlowCoordinator.MultiplayerMode();
    }


    public void BackToMainMenu()
    {
        FlowCoordinator.BackToMainMenu();
    }
}
