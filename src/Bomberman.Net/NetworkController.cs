using System;
using System.Collections.Generic;
using System.Net;
using Bomberman.Core;
using Bomberman.Net.Packets;
using Bomberman.Net.Protocol;

namespace Bomberman.Net
{
    /// <summary>
    /// The high-level network brain that sits on top of <see cref="ITransport"/>. It turns raw bytes
    /// into typed events, runs the housekeeping that keeps a connection alive (heartbeats, ping/pong
    /// RTT, timeout detection), and exposes clean Send* methods so game code never touches sockets.
    /// Topology lives here: a client sends to its host; a host broadcasts to all clients.
    /// Back-ported from Chronos.Net.NetworkController (the Chronos rename is Week 5).
    /// </summary>
    public class NetworkController<TInput> where TInput : struct, IInputState<TInput>
    {
        private readonly ITransport _transport;
        private readonly List<IPEndPoint> _connectedClients = new List<IPEndPoint>();
        public IReadOnlyList<IPEndPoint> ConnectedClients => _connectedClients;

        // Typed events the App subscribes to instead of parsing packets itself.
        public event Action<int, int, int>? OnWelcomeReceived;                 // assignedId, seed, totalPlayers
        public event Action<int, int, int>? OnLobbyUpdateReceived;             // connected, total, slotMask
        public event Action<int, int>? OnStartGameReceived;                    // seed, totalPlayers
        public event Action<IPEndPoint, string, int, int>? OnDiscoveryRequestReceived;
        public event Action<IPEndPoint, string, int, int>? OnDiscoveryResponseReceived;
        public event Action<IPEndPoint>? OnJoinRequestRaw;
        public event Action<int, int, TInput[], int, int, int>? OnInputReceived; // pid, startFrame, inputs, x, y, hash
        public event Action<IPEndPoint, string>? OnDisconnected;
        public event Action<int, bool>? OnLobbyReadyReceived;
        public event Action<byte[]>? OnStateSyncReceived;

        private readonly Dictionary<IPEndPoint, DateTime> _lastDataReceived = new Dictionary<IPEndPoint, DateTime>();
        private readonly PacketReassembler _reassembler = new PacketReassembler();
        private DateTime _lastHeartbeatSent = DateTime.MinValue;
        private DateTime _lastPingSent = DateTime.MinValue;
        private bool _isClient = false;
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1.0);

        /// <summary>Most recent measured round-trip time in milliseconds. The lockstep layer reads
        /// this to choose its input delay.</summary>
        public int LastPingMs { get; private set; } = 0;

        private readonly int _heartbeatInterval = 1000; // ms
        private readonly int _connectionTimeout = 5000; // ms
        private readonly int _chunkSize = 1024;

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
                Console.WriteLine($"[Bomberman.Net] Client Added: {client}");
                _lastDataReceived[client] = DateTime.Now;
            }
        }

        public bool RemoveClient(IPEndPoint client)
        {
            if (_connectedClients.Contains(client))
            {
                _connectedClients.Remove(client);
                _lastDataReceived.Remove(client);
                Console.WriteLine($"[Bomberman.Net] Client Removed: {client}");
                return true;
            }
            return false;
        }

        /// <summary>Pump once per frame: drain the socket, send keep-alives/pings, and reap any peer
        /// that has gone silent past the timeout.</summary>
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
            var timedOut = new List<IPEndPoint>();
            foreach (var kvp in _lastDataReceived)
            {
                if (now - kvp.Value > TimeSpan.FromMilliseconds(_connectionTimeout))
                    timedOut.Add(kvp.Key);
            }
            foreach (var ep in timedOut)
            {
                Console.WriteLine($"[Bomberman.Net] Timeout: {ep}");
                RemoveClient(ep);
                OnDisconnected?.Invoke(ep, "Timed Out");
            }
        }

        public void Close()
        {
            if (_isClient)
            {
                try { _transport.SendToConnectedHost(NetworkProtocol<TInput>.CreateDisconnect("Quit")); } catch { }
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
                _transport.SendTo(packet, new IPEndPoint(IPAddress.Broadcast, p));
        }

        public void SendDiscoveryResponse(IPEndPoint target, string serverName, int currentPlayers, int maxPlayers)
            => _transport.SendTo(NetworkProtocol<TInput>.CreateDiscoveryResponse(serverName, currentPlayers, maxPlayers), target);

        public void RelayPacket(IPEndPoint target, byte[] packet) => _transport.SendTo(packet, target);

        /// <summary>Sends a full snapshot split into 1KB chunks (the reassembler rebuilds it).</summary>
        public void SendStateSync(IPEndPoint target, byte[] snapshot)
        {
            int totalChunks = (int)Math.Ceiling(snapshot.Length / (double)_chunkSize);
            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * _chunkSize;
                int len = Math.Min(_chunkSize, snapshot.Length - offset);
                byte[] chunkData = new byte[len];
                Array.Copy(snapshot, offset, chunkData, 0, len);
                _transport.SendTo(NetworkProtocol<TInput>.CreateStateChunk(i, totalChunks, chunkData), target);
                System.Threading.Thread.Sleep(2); // crude pacing so we don't overrun the socket buffer
            }
        }

        public void SendDisconnect(IPEndPoint target, string reason)
            => _transport.SendTo(NetworkProtocol<TInput>.CreateDisconnect(reason), target);

        /// <summary>Sends a player's input history. Player 0 is the host (broadcasts); others send to host.</summary>
        public void SendInput(int pid, int frame, TInput[] history, int x, int y, int hash)
        {
            byte[] packet = NetworkProtocol<TInput>.CreateInputPacket(pid, frame, history, x, y, hash);
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
        public void SendLobbyReadyTo(IPEndPoint target, int pid, bool isReady)
            => _transport.SendTo(NetworkProtocol<TInput>.CreateLobbyReady(pid, isReady), target);

        // --- Internals ---

        private void Broadcast(byte[] data)
        {
            foreach (var client in _connectedClients)
                _transport.SendTo(data, client);
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
                    Console.WriteLine($"[Bomberman.Net] Disconnect from {sender}: {reason}");
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
                    _reassembler.HandleChunk(sender, chunkIndex, totalChunks, chunkData,
                        full => OnStateSyncReceived?.Invoke(full));
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

                case PacketType.LobbyReady:
                    var (readyPid, isReady) = NetworkProtocol<TInput>.ReadLobbyReady(data);
                    OnLobbyReadyReceived?.Invoke(readyPid, isReady);
                    break;

                case PacketType.Ping:
                    long pTimestamp = NetworkProtocol<TInput>.ReadPing(data);
                    _transport.SendTo(NetworkProtocol<TInput>.CreatePong(pTimestamp), sender);
                    break;

                case PacketType.Pong:
                    long pongTimestamp = NetworkProtocol<TInput>.ReadPong(data);
                    long rttTicks = DateTime.Now.Ticks - pongTimestamp;
                    LastPingMs = (int)(rttTicks / TimeSpan.TicksPerMillisecond);
                    break;
            }
        }
    }
}
