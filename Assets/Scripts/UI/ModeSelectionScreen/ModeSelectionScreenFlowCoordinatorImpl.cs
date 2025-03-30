using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelectionScreenFlowCoordinatorImpl : ModeSelectionScreenFlowCoordinator
{
    public void MultiplayerMode() {
        Debug.Log("Multiplayer mode start");
        SceneManager.LoadScene("MuliplayerScreen");
    }

    public void SingleplayerMode()
    {
        //SceneManager.LoadScene("DifficultyScreen");
        Debug.Log("Difficulty screen show");
    }

    public void BackToMainMenu()
    {
        Debug.Log("Back to main menu");
        //SceneManager.LoadScene("MainMenuScene");
    }
}