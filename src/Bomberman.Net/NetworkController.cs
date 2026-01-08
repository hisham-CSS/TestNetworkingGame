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
        public UdpTransport Transport { get; private set; }
        
        // Events to decouple logic from Program.cs
        public event Action<int, int, int>? OnWelcomeReceived; // assignedId, seed, totalPlayers
        public event Action<int, int>? OnLobbyUpdateReceived; // connectedCount, totalPlayers
        public event Action<int, int>? OnStartGameReceived; // seed, totalPlayers
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryRequestReceived; // sender, header, players, max
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryResponseReceived; // sender, name, players, max
        // OnJoinRequestReceived removed
        public event Action<System.Net.IPEndPoint>? OnJoinRequestRaw; // Just notify that someone wants to join
        
        public event Action<int, int, InputState[], Vector2, int>? OnInputReceived; // pid, frame, history, pos, hash

        public NetworkController(int port)
        {
            Transport = new UdpTransport(port);
            Transport.OnPacketReceived += HandlePacket;
        }

        public void Update()
        {
            Transport.Poll();
        }

        public void Close()
        {
            Transport.Close();
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

            // Logic from RollbackSystem.Update:
            // if (_localPlayerId == 0) transport.Broadcast(packet);
            // else transport.Send(packet);
            
            // Wait, NetworkController doesn't know "local player id" explicitly? 
            // It just has Transport.
            // But usually Host (pid 0) should Broadcast, Client should Send (Unicast to Host).
            // However, UdpTransport.Broadcast sends to ALL connected clients.
            // If I am Host (0), I broadcast.
            // If I am Client, I send to Server.
            
            // NetworkController constructor doesn't take PID.
            // But we can check bundle.PlayerId?
            // If bundle.PlayerId == 0 -> Broadcast?
            // Safer: External caller decides? Or we add param to SendInput?
            // Or assume PID 0 is always Host.
            
            if (bundle.PlayerId == 0) 
            {
                Transport.Broadcast(packet);
            }
            else 
            {
                Transport.Send(packet);
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
