using UnityEngine;
using UnityEngine.SceneManagement;

public class DifficultyScreenFlowCoordinatorImpl: DifficultyScreenFlowCoordinator {
    public void EasyMode() {
        Debug.Log("Easy mode start");
    }

    public void MediumMode()
    {
        Debug.Log("Medium mode start");
    }

    public void HardMode()
    {
        Debug.Log("Hard mode start");
    }

    public void BackToModeSelection()
    {
        Debug.Log("Back to mode selection");
        //SceneManager.LoadScene("ModeSelectionScreen");
    }
}