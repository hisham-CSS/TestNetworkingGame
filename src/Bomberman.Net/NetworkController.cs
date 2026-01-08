using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Xna.Framework;
using Bomberman.Core;
using Bomberman.Core.Rollback;

namespace Bomberman.Net
{
    public class NetworkController
    {
        private UdpTransport _transport;
        public IEnumerable<IPEndPoint> ConnectedClients => _transport.ConnectedClients;

        // Events to decouple logic from Program.cs
        public event Action<int, int, int>? OnWelcomeReceived; // assignedId, seed, totalPlayers
        public event Action<int, int>? OnLobbyUpdateReceived; // connectedCount, totalPlayers
        public event Action<int, int>? OnStartGameReceived; // seed, totalPlayers
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryRequestReceived; // sender, header, players, max
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryResponseReceived; // sender, name, players, max
        public event Action<System.Net.IPEndPoint>? OnJoinRequestRaw; // Just notify that someone wants to join
        
        public event Action<int, int, InputState[], IntVector2, int>? OnInputReceived; // pid, frame, history, pos, hash

        public NetworkController(int port)
        {
            _transport = new UdpTransport(port);
            _transport.OnPacketReceived += HandlePacket;
        }

        public void Connect(string ip, int port)
        {
            _transport.Connect(ip, port);
        }

        public void AddClient(IPEndPoint client)
        {
            _transport.AddClient(client);
        }

        public void Update()
        {
            _transport.Poll();
        }

        public void Close()
        {
            _transport.Close();
        }

        public void SendJoinRequest()
        {
            _transport.Send(NetworkProtocol.CreateJoinRequest());
        }

        public void SendWelcome(IPEndPoint target, int assignedId, int seed, int totalPlayers)
        {
            _transport.SendTo(NetworkProtocol.CreateWelcome(assignedId, seed, totalPlayers), target);
        }

        public void BroadcastLobbyUpdate(int connectedCount, int totalPlayers)
        {
            _transport.Broadcast(NetworkProtocol.CreateLobbyUpdate(connectedCount, totalPlayers));
        }

        public void BroadcastStartGame(int seed, int totalPlayers)
        {
            _transport.Broadcast(NetworkProtocol.CreateStartGame(seed, totalPlayers));
        }

        public void BroadcastDiscoveryRequest(int startPort, int endPort)
        {
            var packet = NetworkProtocol.CreateDiscoveryRequest();
            for (int p = startPort; p < endPort; p++)
            {
                _transport.BroadcastToPort(packet, p);
            }
        }

        public void SendDiscoveryResponse(IPEndPoint target, string serverName, int currentPlayers, int maxPlayers)
        {
            _transport.SendTo(NetworkProtocol.CreateDiscoveryResponse(serverName, currentPlayers, maxPlayers), target);
        }

        public void RelayPacket(IPEndPoint target, byte[] packet)
        {
            _transport.SendTo(packet, target);
        }

        public void SendInput(OutgoingInputBundle bundle)
        {
            byte[] packet = NetworkProtocol.CreateInputPacket(
                bundle.PlayerId, 
                bundle.Frame, 
                bundle.RedundantHistory, 
                bundle.LocalPosition, 
                bundle.LocalStateHash
            );
            
            if (bundle.PlayerId == 0) 
            {
                _transport.Broadcast(packet);
            }
            else 
            {
                _transport.Send(packet);
            }
        }

        private void HandlePacket(byte[] data, IPEndPoint sender)
        {
            if (data.Length == 0) return;

            PacketType type = (PacketType)data[0];

            switch (type)
            {
                case PacketType.DiscoveryRequest:
                    OnDiscoveryRequestReceived?.Invoke(sender, "Request", 0, 0); 
                    break;

                case PacketType.DiscoveryResponse:
                    var (name, cur, max) = NetworkProtocol.ReadDiscoveryResponse(data);
                    OnDiscoveryResponseReceived?.Invoke(sender, name, cur, max);
                    break;

                case PacketType.JoinRequest:
                    // logic in Program.cs was: check if not full, add client, send welcome, broadcast update
                    // We can just expose the Raw Endpoint for the Host to decide
                    OnJoinRequestRaw?.Invoke(sender);
                    break;

                case PacketType.Welcome:
                    var (assignedId, seed, total) = NetworkProtocol.ReadWelcome(data);
                    OnWelcomeReceived?.Invoke(assignedId, seed, total);
                    break;

                case PacketType.LobbyUpdate:
                    var (connected, totalP) = NetworkProtocol.ReadLobbyUpdate(data);
                    OnLobbyUpdateReceived?.Invoke(connected, totalP);
                    break;

                case PacketType.StartGame:
                    var (gameSeed, gameTotal) = NetworkProtocol.ReadStartGame(data);
                    OnStartGameReceived?.Invoke(gameSeed, gameTotal);
                    break;

                case PacketType.Input:
                    var (pid, frame, inputs, pos, hash) = NetworkProtocol.ReadInputPacket(data);
                    OnInputReceived?.Invoke(pid, frame, inputs, pos, hash);
                    break;
            }
        }
    }
}
