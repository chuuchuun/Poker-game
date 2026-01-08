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
    private const string MsgOpen = "OPEN";
    private const string MsgClose = "CLOSE";

    private const int LobbyStaleTimeoutMs = 3500;

    private static LANLobbyManager _instance;
    public static LANLobbyManager Instance => _instance ??= new LANLobbyManager();

    public List<LobbyInfo> AvailableLobbies { get; private set; } = new List<LobbyInfo>();
    private readonly object lobbyLock = new object();
    private readonly Dictionary<string, long> lobbyLastSeenTicks = new Dictionary<string, long>();

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

    public void StartHostLAN()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData("0.0.0.0", (ushort)GamePort);

        serverIPAddress = GetLocalIPAddress();

        NetworkManager.Singleton.StartHost();
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
            broadcaster.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);

            while (isBroadcasting)
            {
                try
                {
                    byte[] data = BuildLobbyBroadcastPacket(MsgOpen);
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

    private byte[] BuildLobbyBroadcastPacket(string messageType)
    {
        int currentPlayers = NetworkManager.Singleton?.ConnectedClientsList.Count ?? 0;
        int maxPlayers = 6;

        string packet = $"{messageType}|{LobbyName}|{currentPlayers}|{maxPlayers}|{serverIPAddress}";
        return Encoding.UTF8.GetBytes(packet);
    }

    private void SendLobbyCloseBroadcast(int repeatCount = 3)
    {
        if (string.IsNullOrEmpty(serverIPAddress))
        {
            serverIPAddress = GetLocalIPAddress();
        }

        try
        {
            using (UdpClient broadcaster = new UdpClient())
            {
                broadcaster.EnableBroadcast = true;
                broadcaster.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
                byte[] data = BuildLobbyBroadcastPacket(MsgClose);

                for (int i = 0; i < repeatCount; i++)
                {
                    broadcaster.Send(data, data.Length, endPoint);
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"Close broadcast error: {e.Message}");
        }
    }

    public void StopBroadcasting()
    {
        SendLobbyCloseBroadcast();

        isBroadcasting = false;

        try
        {
            broadcastThread?.Join(500);
        }
        catch { }

        broadcastThread = null;
        ClearLobbies();
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
        receivingSocket.Client.ReceiveTimeout = 1000;

        listenerClient = receivingSocket;

        while (isListening)
        {
            try
            {
                byte[] data = receivingSocket.Receive(ref from);
                string packet = Encoding.UTF8.GetString(data);
                string[] parts = packet.Split('|');

                if (parts.Length < 3)
                    continue;

                string messageType = MsgOpen;
                int offset = 0;
                if (string.Equals(parts[0], MsgOpen, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(parts[0], MsgClose, StringComparison.OrdinalIgnoreCase))
                {
                    messageType = parts[0].ToUpperInvariant();
                    offset = 1;
                }

                if (parts.Length < (3 + offset))
                    continue;

                string lobbyName = parts[0 + offset];
                if (!int.TryParse(parts[1 + offset], out int currentPlayers))
                    continue;
                if (!int.TryParse(parts[2 + offset], out int maxPlayers))
                    continue;

                string lobbyIP = parts.Length >= (4 + offset) ? parts[3 + offset] : from.Address.ToString();
                string lobbyId = $"{lobbyIP}:{GamePort}";

                lock (lobbyLock)
                {
                    lobbyLastSeenTicks[lobbyId] = DateTime.UtcNow.Ticks;

                    if (messageType == MsgClose)
                    {
                        AvailableLobbies.RemoveAll(l => l.LobbyId == lobbyId);
                        lobbyLastSeenTicks.Remove(lobbyId);
                        continue;
                    }

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

                    EvictStaleLobbies_NoLock();
                }
            }
            catch (SocketException)
            {
                lock (lobbyLock)
                {
                    EvictStaleLobbies_NoLock();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"Listen error: {e.Message}");
                if (!isListening) break;
            }
        }

        receivingSocket.Close();
        if (ReferenceEquals(listenerClient, receivingSocket))
        {
            listenerClient = null;
        }
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
            lobbyLastSeenTicks.Clear();
        }
    }

    private void EvictStaleLobbies_NoLock()
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        long timeoutTicks = LobbyStaleTimeoutMs * TimeSpan.TicksPerMillisecond;

        if (lobbyLastSeenTicks.Count == 0)
            return;

        List<string> staleIds = null;
        foreach (var kvp in lobbyLastSeenTicks)
        {
            if (nowTicks - kvp.Value > timeoutTicks)
            {
                staleIds ??= new List<string>();
                staleIds.Add(kvp.Key);
            }
        }

        if (staleIds == null)
            return;

        foreach (var id in staleIds)
        {
            AvailableLobbies.RemoveAll(l => l.LobbyId == id);
            lobbyLastSeenTicks.Remove(id);
        }
    }
}