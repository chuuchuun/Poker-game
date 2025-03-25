using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerScreen : MonoBehaviour
{
    private MultiplayerScreenFlowCoordinator FlowCoordinator;
    MultiplayerScreen() {
        FlowCoordinator = new MultiplayerScreenFlowCoordinatorImpl();
    }

    MultiplayerScreen(MultiplayerScreenFlowCoordinator multiplayerScreenFlowCoordinator) {
        FlowCoordinator = multiplayerScreenFlowCoordinator;
    }

    public void CreateGame() {
        FlowCoordinator.CreateGame();
    }

    public void JoinGame() {
        FlowCoordinator.JoinGame();
    }


    public void BackToModeSelection()
    {
        FlowCoordinator.BackToModeSelection();
    }
}
