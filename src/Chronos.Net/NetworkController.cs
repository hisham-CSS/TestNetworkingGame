using System;
using System.Collections.Generic;
using System.Net;
using Chronos.Core;
using Chronos.Net.Packets;
using Chronos.Net.Protocol;

namespace Chronos.Net
{
    /// <summary>
    /// Controls the high-level network logic for a generic game.
    /// Manages connections, packet routing, and state synchronization.
    /// </summary>
    /// <typeparam name="TInput">The input struct type for the specific game.</typeparam>
    public class NetworkController<TInput> where TInput : struct, IInputState<TInput>
    {
        private ITransport _transport;
        private List<IPEndPoint> _connectedClients = new List<IPEndPoint>();
        
        public IReadOnlyList<IPEndPoint> ConnectedClients => _connectedClients;

        // Events
        public event Action<int, int, int>? OnWelcomeReceived;
        public event Action<int, int, int>? OnLobbyUpdateReceived;
        public event Action<int, int>? OnStartGameReceived;
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryRequestReceived;
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryResponseReceived; 
        public event Action<System.Net.IPEndPoint>? OnJoinRequestRaw; 
        public event Action<int, int, TInput[], int, int, int>? OnInputReceived; // inputs, posx, posy, hash
        public event Action<IPEndPoint, string>? OnDisconnected; 
        public event Action<int, bool>? OnLobbyReadyReceived;
        public event Action<byte[]>? OnStateSyncReceived;

        private Dictionary<IPEndPoint, DateTime> _lastDataReceived = new Dictionary<IPEndPoint, DateTime>();
        private PacketReassembler _reassembler = new PacketReassembler();
        private DateTime _lastHeartbeatSent = DateTime.MinValue;
        private bool _isClient = false;
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1.0);
        public int LastPingMs { get; private set; } = 0;
        private DateTime _lastPingSent = DateTime.MinValue;

        // TODO: Pass in Config
        private int _heartbeatInterval = 1000;
        private int _connectionTimeout = 5000;
        private int _chunkSize = 1024;

        public NetworkController(ITransport transport)
        {
            _transport = transport;
            _transport.PacketReceived += HandlePacket;
        }

        public void Connect(string ip, int port)
        {
            _transport.Connect(ip, port);
            _isClient = true;
        }

        public void AddClient(IPEndPoint client)
        {
            if (!_connectedClients.Contains(client))
            {
                _connectedClients.Add(client);
                Console.WriteLine($"[Chronos.Net] Client Added: {client}");
                _lastDataReceived[client] = DateTime.Now;
            }
        }

        public bool RemoveClient(IPEndPoint client)
        {
            if (_connectedClients.Contains(client))
            {
                _connectedClients.Remove(client);
                _lastDataReceived.Remove(client);
                Console.WriteLine($"[Chronos.Net] Client Removed: {client}");
                return true;
            }
            return false;
        }

        public void Update()
        {
            _transport.Poll();

            if (DateTime.Now - _lastHeartbeatSent > TimeSpan.FromMilliseconds(_heartbeatInterval))
            {
                var hb = NetworkProtocol<TInput>.CreateHeartbeat();
                if (_isClient) _transport.SendToConnectedHost(hb);
                else Broadcast(hb);
                _lastHeartbeatSent = DateTime.Now;
            }

            if (DateTime.Now - _lastPingSent > PingInterval)
            {
               long timestamp = DateTime.Now.Ticks;
               var ping = NetworkProtocol<TInput>.CreatePing(timestamp);
               if (_isClient) _transport.SendToConnectedHost(ping);
               else Broadcast(ping);
               _lastPingSent = DateTime.Now;
            }

            var now = DateTime.Now;
            List<IPEndPoint> timedOut = new List<IPEndPoint>();
            foreach(var kvp in _lastDataReceived)
            {
                if (now - kvp.Value > TimeSpan.FromMilliseconds(_connectionTimeout))
                {
                    timedOut.Add(kvp.Key);
                }
            }

            foreach(var ep in timedOut)
            {
                 Console.WriteLine($"[Chronos.Net] Timeout: {ep}");
                 RemoveClient(ep);
                 OnDisconnected?.Invoke(ep, "Timed Out");
            }
        }

        public void Close()
        {
            if (_isClient)
            {
                try { _transport.SendToConnectedHost(NetworkProtocol<TInput>.CreateDisconnect("Quit")); } catch {}
            }
            else
            {
                Broadcast(NetworkProtocol<TInput>.CreateDisconnect("Host Quit"));
            }
            _transport.Dispose();
        }

        // --- Senders ---

        public void SendJoinRequest() => _transport.SendToConnectedHost(NetworkProtocol<TInput>.CreateJoinRequest());
        public void SendWelcome(IPEndPoint target, int assignedId, int seed, int totalPlayers) 
            => _transport.SendTo(NetworkProtocol<TInput>.CreateWelcome(assignedId, seed, totalPlayers), target);
        public void BroadcastLobbyUpdate(int connectedCount, int totalPlayers, int slotMask) 
            => Broadcast(NetworkProtocol<TInput>.CreateLobbyUpdate(connectedCount, totalPlayers, slotMask));
        public void BroadcastStartGame(int seed, int totalPlayers) 
            => Broadcast(NetworkProtocol<TInput>.CreateStartGame(seed, totalPlayers));
        
        public void BroadcastDiscoveryRequest(int startPort, int endPort)
        {
            var packet = NetworkProtocol<TInput>.CreateDiscoveryRequest();
            for (int p = startPort; p < endPort; p++)
            {
                _transport.SendTo(packet, new IPEndPoint(IPAddress.Broadcast, p));
            }
        }

        public void SendDiscoveryResponse(IPEndPoint target, string serverName, int currentPlayers, int maxPlayers) 
            => _transport.SendTo(NetworkProtocol<TInput>.CreateDiscoveryResponse(serverName, currentPlayers, maxPlayers), target);

        public void RelayPacket(IPEndPoint target, byte[] packet) => _transport.SendTo(packet, target);

        public void SendStateSync(IPEndPoint target, byte[] snapshot)
        {
            int totalChunks = (int)Math.Ceiling(snapshot.Length / (double)_chunkSize);
            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * _chunkSize;
                int len = Math.Min(_chunkSize, snapshot.Length - offset);
                byte[] chunkData = new byte[len];
                Array.Copy(snapshot, offset, chunkData, 0, len);
                
                byte[] packet = NetworkProtocol<TInput>.CreateStateChunk(i, totalChunks, chunkData);
                _transport.SendTo(packet, target);
                System.Threading.Thread.Sleep(2); 
            }
        }

        public void SendDisconnect(IPEndPoint target, string reason) => _transport.SendTo(NetworkProtocol<TInput>.CreateDisconnect(reason), target);

        public void SendInput(int pid, int frame, TInput[] history, int x, int y, int hash)
        {
            // Week 5: send the run-length compressed input packet (bandwidth optimization).
            byte[] packet = NetworkProtocol<TInput>.CreateCompressedInputPacket(pid, frame, history, x, y, hash);
            if (pid == 0) Broadcast(packet);
            else _transport.SendToConnectedHost(packet);
        }

        public void SendLobbyReady(int pid, bool isReady)
        {
             var packet = NetworkProtocol<TInput>.CreateLobbyReady(pid, isReady);
             if (_isClient) _transport.SendToConnectedHost(packet);
             else Broadcast(packet);
        }

        public void BroadcastLobbyReady(int pid, bool isReady) => Broadcast(NetworkProtocol<TInput>.CreateLobbyReady(pid, isReady));
        public void SendLobbyReadyTo(IPEndPoint target, int pid, bool isReady) => _transport.SendTo(NetworkProtocol<TInput>.CreateLobbyReady(pid, isReady), target);


        // --- Internals ---

        private void Broadcast(byte[] data)
        {
            foreach(var client in _connectedClients)
            {
                _transport.SendTo(data, client);
            }
        }

        private void HandlePacket(byte[] data, IPEndPoint sender)
        {
            if (data.Length == 0) return;
            _lastDataReceived[sender] = DateTime.Now;
            
            PacketType type = NetworkProtocol<TInput>.ReadType(data);

            switch (type)
            {
                case PacketType.Heartbeat:
                    break;
                
                case PacketType.Disconnect:
                    string reason = NetworkProtocol<TInput>.ReadDisconnect(data);
                    Console.WriteLine($"[Chronos.Net] Disconnect from {sender}: {reason}");
                    bool wasRemoved = RemoveClient(sender);
                    if (wasRemoved || _isClient) OnDisconnected?.Invoke(sender, reason);
                    break;

                case PacketType.DiscoveryRequest:
                    OnDiscoveryRequestReceived?.Invoke(sender, "Request", 0, 0); 
                    break;

                case PacketType.DiscoveryResponse:
                    var (name, cur, max) = NetworkProtocol<TInput>.ReadDiscoveryResponse(data);
                    OnDiscoveryResponseReceived?.Invoke(sender, name, cur, max);
                    break;

                case PacketType.JoinRequest:
                    int version = NetworkProtocol<TInput>.ReadJoinRequest(data);
                    if (version != NetworkProtocol<TInput>.ProtocolVersion) return;
                    OnJoinRequestRaw?.Invoke(sender);
                    break;
                    
                case PacketType.StateChunk:
                    var (chunkIndex, totalChunks, chunkData) = NetworkProtocol<TInput>.ReadStateChunk(data);
                    _reassembler.HandleChunk(sender, chunkIndex, totalChunks, chunkData, (fullSnapshot) => {
                         OnStateSyncReceived?.Invoke(fullSnapshot);
                    });
                    break;
                    
                case PacketType.StateSync:
                    byte[] snap = NetworkProtocol<TInput>.ReadStateSync(data);
                    OnStateSyncReceived?.Invoke(snap);
                    break;

                case PacketType.Welcome:
                    var (assignedId, seed, total) = NetworkProtocol<TInput>.ReadWelcome(data);
                    OnWelcomeReceived?.Invoke(assignedId, seed, total);
                    break;

                case PacketType.LobbyUpdate:
                    var (connected, totalP, mask) = NetworkProtocol<TInput>.ReadLobbyUpdate(data);
                    OnLobbyUpdateReceived?.Invoke(connected, totalP, mask);
                    break;

                case PacketType.StartGame:
                    var (gameSeed, gameTotal) = NetworkProtocol<TInput>.ReadStartGame(data);
                    OnStartGameReceived?.Invoke(gameSeed, gameTotal);
                    break;

                case PacketType.Input:
                    var (pid, frame, inputs, px, py, hash) = NetworkProtocol<TInput>.ReadInputPacket(data);
                    OnInputReceived?.Invoke(pid, frame, inputs, px, py, hash);
                    break;

                case PacketType.InputCompressed:
                    var (cpid, cframe, cinputs, cpx, cpy, chash) = NetworkProtocol<TInput>.ReadCompressedInputPacket(data);
                    OnInputReceived?.Invoke(cpid, cframe, cinputs, cpx, cpy, chash);
                    break;

                case PacketType.LobbyReady:
                    var (readyPid, isReady) = NetworkProtocol<TInput>.ReadLobbyReady(data);
                    OnLobbyReadyReceived?.Invoke(readyPid, isReady);
                    break;

                case PacketType.Ping:
                    long pTimestamp = NetworkProtocol<TInput>.ReadPing(data);
                    var pong = NetworkProtocol<TInput>.CreatePong(pTimestamp);
                    _transport.SendTo(pong, sender);
                    break;

                case PacketType.Pong:
                    long pontTimestamp = NetworkProtocol<TInput>.ReadPong(data);
                    long rttTicks = DateTime.Now.Ticks - pontTimestamp;
                    LastPingMs = (int)(rttTicks / TimeSpan.TicksPerMillisecond);
                    break;
            }
        }
    }
}
