[System.Serializable]
public class LobbyInfo
{
    public string LobbyId { get; set; }
    public string LobbyName { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }

    public LobbyInfo(string id, string name, int currentPlayers, int maxPlayers)
    {
        LobbyId = id;
        LobbyName = name;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
    }
}
