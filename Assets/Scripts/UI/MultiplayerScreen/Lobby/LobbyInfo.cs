using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LobbyInfo
{
    public string LobbyId;
    public string LobbyName;
    public int CurrentPlayers;

    public LobbyInfo(string id, string name, int current, int max)
    {
        LobbyId = id;
        LobbyName = name;
        CurrentPlayers = current;
    }
}
