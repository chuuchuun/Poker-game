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
    public string LobbyName { get; set; } = "My LAN Lobby";

    private UdpClient listenerClient;
    private Thread listenThread;
    private bool isListening = false;

    private Thread broadcastThread;
    private bool isBroadcasting = false;

    private LANLobbyManager() { }
    public void StartHostLAN()
    {
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

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);
            byte[] data = Encoding.UTF8.GetBytes(LobbyName);

            while (isBroadcasting)
            {
                try
                {
                    broadcaster.Send(data, data.Length, endPoint);
                }
                catch {}

                Thread.Sleep(1000);
            }

            broadcaster.Close();
        });

        broadcastThread.IsBackground = true;
        broadcastThread.Start();
    }

    public void StopBroadcasting()
    {
        isBroadcasting = false;
        broadcastThread?.Join();
    }

    public void StartListeningForLobbies()
    {
        if (isListening) return;

        listenerClient = new UdpClient(0);
        listenerClient.EnableBroadcast = true;

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
                string lobbyName = Encoding.UTF8.GetString(data);

                lock (lobbyLock)
                {
                    bool exists = AvailableLobbies.Exists(l => l.LobbyId == from.Address.ToString());
                    if (!exists)
                    {
                        AvailableLobbies.Add(new LobbyInfo(
                            id: from.Address.ToString(),
                            name: lobbyName,
                            current: 0,
                            max: 4
                        ));
                    }
                }
            }
            catch
            {
                break;
            }
        }

        receivingSocket.Close();
    }

    public void StopListening()
    {
        isListening = false;
        listenThread?.Join();

        listenerClient?.Close();
        listenerClient = null;
    }

    public void JoinGameLAN(string ipAddress)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = ipAddress;
        transport.ConnectionData.Port = 7777;

        NetworkManager.Singleton.StartClient();
    }
}
