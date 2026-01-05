using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class LANLobbyManager
{
    private static LANLobbyManager _instance;
    public static LANLobbyManager Instance => _instance ??= new LANLobbyManager();

    public List<LobbyInfo> AvailableLobbies { get; private set; } = new List<LobbyInfo>();
    private readonly object lobbyLock = new object();

    public int BroadcastPort { get; set; } = 47777;
    public int GamePort { get; set; } = 7777;
    public string LobbyName { get; set; } = "My LAN Lobby";

    private UdpClient listenerClient;
    private Thread listenThread;
    private bool isListening = false;

    private Thread broadcastThread;
    private bool isBroadcasting = false;

    private string serverIPAddress;

    private LANLobbyManager() { }

    public void PrepareHostTransport()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("0.0.0.0", (ushort)GamePort);

        serverIPAddress = GetLocalIPAddress();
    }

    public void StartHostLAN()
    {
        PrepareHostTransport();

        if (!NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.StartHost();
        }

        StartBroadcasting();
    }

    private void StartBroadcasting()
    {
        if (isBroadcasting) return;

        isBroadcasting = true;

        broadcastThread = new Thread(() =>
        {
            UdpClient broadcaster = new UdpClient();
            broadcaster.EnableBroadcast = true;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);

            while (isBroadcasting)
            {
                try
                {
                    byte[] data = BuildLobbyBroadcastPacket();
                    broadcaster.Send(data, data.Length, endPoint);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"Broadcast error: {e.Message}");
                }

                Thread.Sleep(1000);
            }

            broadcaster.Close();
        });

        broadcastThread.IsBackground = true;
        broadcastThread.Start();
    }

    private byte[] BuildLobbyBroadcastPacket()
    {
        int currentPlayers = NetworkManager.Singleton?.ConnectedClientsList.Count ?? 0;
        int maxPlayers = 6;

        string packet = $"{LobbyName}|{currentPlayers}|{maxPlayers}|{serverIPAddress}";
        return Encoding.UTF8.GetBytes(packet);
    }

    public void StopBroadcasting()
    {
        isBroadcasting = false;
        broadcastThread?.Join();
        broadcastThread = null;
    }

    public void StartListeningForLobbies()
    {
        if (isListening) return;

        AvailableLobbies.Clear();
        isListening = true;

        listenThread = new Thread(ListenLoop);
        listenThread.IsBackground = true;
        listenThread.Start();
    }

    private void ListenLoop()
    {
        IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);

        UdpClient receivingSocket = new UdpClient();
        receivingSocket.EnableBroadcast = true;
        receivingSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        receivingSocket.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));

        while (isListening)
        {
            try
            {
                byte[] data = receivingSocket.Receive(ref from);
                string packet = Encoding.UTF8.GetString(data);
                string[] parts = packet.Split('|');

                if (parts.Length >= 3)
                {
                    string lobbyName = parts[0];
                    int currentPlayers = int.Parse(parts[1]);
                    int maxPlayers = int.Parse(parts[2]);

                    string lobbyIP = parts.Length >= 4 ? parts[3] : from.Address.ToString();

                    string lobbyId = $"{lobbyIP}:{GamePort}";

                    lock (lobbyLock)
                    {
                        var existing = AvailableLobbies.Find(l => l.LobbyId == lobbyId);

                        if (existing == null)
                        {
                            AvailableLobbies.Add(new LobbyInfo(
                                id: lobbyId,
                                name: lobbyName,
                                currentPlayers: currentPlayers,
                                maxPlayers: maxPlayers,
                                ipAddress: lobbyIP
                            ));
                        }
                        else
                        {
                            existing.LobbyName = lobbyName;
                            existing.CurrentPlayers = currentPlayers;
                            existing.MaxPlayers = maxPlayers;
                            existing.IPAddress = lobbyIP;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Listen error: {e.Message}");
                if (!isListening) break;
            }
        }

        receivingSocket.Close();
    }

    public void StopListening()
    {
        isListening = false;

        try
        {
            using (var tempClient = new UdpClient())
            {
                tempClient.Connect("127.0.0.1", BroadcastPort);
                tempClient.Send(new byte[0], 0);
            }
        }
        catch { }

        listenThread?.Join(1000);
        listenThread = null;

        listenerClient?.Close();
        listenerClient = null;
    }

    public void JoinGameLAN(string ipAddress)
    {
        string cleanIP = ipAddress;
        if (ipAddress.Contains(":"))
        {
            cleanIP = ipAddress.Split(':')[0];
        }

        string connectIP = (cleanIP == GetLocalIPAddress()) ? "127.0.0.1" : cleanIP;

        UnityEngine.Debug.Log($"Joining game at {connectIP}:{GamePort}");

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(connectIP, (ushort)GamePort);

        NetworkManager.Singleton.StartClient();
    }

    public void JoinGameLAN(LobbyInfo lobby)
    {
        JoinGameLAN(lobby.IPAddress);
    }

    private string GetLocalIPAddress()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address?.ToString() ?? "127.0.0.1";
            }
        }
        catch
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }
    }

    public void ClearLobbies()
    {
        lock (lobbyLock)
        {
            AvailableLobbies.Clear();
        }
    }

    public void ResetLanState()
    {
        StopBroadcasting();
        StopListening();
        ClearLobbies();

        if (NetworkManager.Singleton != null)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", (ushort)GamePort);
            }
        }
    }
}
