// Updated LobbyInfo class to ensure proper IP handling
public class LobbyInfo
{
    public string LobbyId { get; set; }
    public string LobbyName { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public string IPAddress { get; set; }

    public LobbyInfo(string id, string name, int currentPlayers, int maxPlayers, string ipAddress = null)
    {
        LobbyId = id;
        LobbyName = name;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;

        if (!string.IsNullOrEmpty(ipAddress) && ipAddress.Contains(":"))
        {
            IPAddress = ipAddress.Split(':')[0];
        }
        else
        {
            IPAddress = ipAddress ?? "127.0.0.1";
        }
    }
}