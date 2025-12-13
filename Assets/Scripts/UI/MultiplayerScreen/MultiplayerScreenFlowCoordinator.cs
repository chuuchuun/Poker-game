using System.Collections.Generic;

interface MultiplayerScreenFlowCoordinator
{
    public void CreateGame(string lobbyName);
    public void JoinGame(string lobbyIP);
    public void BackToModeSelection();

    List<LobbyInfo> GetAvailableLobbies();
}