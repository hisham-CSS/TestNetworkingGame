using System;
using System.Collections.Generic;
using System.Net;
using Bomberman.Core;
using Bomberman.Core.Input;
using Bomberman.Core.Rollback;
using Bomberman.Net.Packets;

namespace Bomberman.Net
{
    public class NetworkController
    {
        private ITransport _transport;
        private List<IPEndPoint> _connectedClients = new List<IPEndPoint>();
        public IReadOnlyList<IPEndPoint> ConnectedClients => _connectedClients;

        // Events to decouple logic from Program.cs
        public event Action<int, int, int>? OnWelcomeReceived; // assignedId, seed, totalPlayers
        public event Action<int, int, int>? OnLobbyUpdateReceived; // connectedCount, totalPlayers, slotMask
        public event Action<int, int>? OnStartGameReceived; // seed, totalPlayers
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryRequestReceived; // sender, header, players, max
        public event Action<System.Net.IPEndPoint, string, int, int>? OnDiscoveryResponseReceived; 
        public event Action<System.Net.IPEndPoint>? OnJoinRequestRaw; 
        public event Action<int, int, InputState[], IntVector2, int>? OnInputReceived; 
        public event Action<IPEndPoint, string>? OnDisconnected; // sender, reason
        public event Action<int, bool>? OnLobbyReadyReceived; // pid, isReady
        public event Action<byte[]>? OnStateSyncReceived;

        private Dictionary<IPEndPoint, DateTime> _lastDataReceived = new Dictionary<IPEndPoint, DateTime>();
        
        // Chunk Reassembly: [Endpoint] -> (Chunks[Index->Data], TotalChunks)
        private Dictionary<IPEndPoint, (Dictionary<int, byte[]> Chunks, int TotalChunks)> _reassemblyBuffers = new Dictionary<IPEndPoint, (Dictionary<int, byte[]>, int)>();
        
        private DateTime _lastHeartbeatSent = DateTime.MinValue;
        private bool _isClient = false;

        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1.0);
        private static readonly TimeSpan TimeoutThreshold = TimeSpan.FromSeconds(60.0); // Increased to support long pauses/window drags
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(1.0);

        public int LastPingMs { get; private set; } = 0;
        private DateTime _lastPingSent = DateTime.MinValue;



        // Allow injecting mock transport for testing
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
                Console.WriteLine($"[Network] Client Added: {client}");
                _lastDataReceived[client] = DateTime.Now;
            }
        }

        public bool RemoveClient(IPEndPoint client)
        {
            if (_connectedClients.Contains(client))
            {
                _connectedClients.Remove(client);
                _lastDataReceived.Remove(client);
                Console.WriteLine($"[Network] Client Removed: {client}");
                return true;
            }
            return false;
        }

        public void Update()
        {
            _transport.Poll();

            // Heartbeat Logic
            if (DateTime.Now - _lastHeartbeatSent > HeartbeatInterval)
            {
                var hb = NetworkProtocol.CreateHeartbeat();
                if (_isClient)
                {
                    _transport.SendToConnectedHost(hb);
                }
                else
                {
                    // Host sends to all clients
                    Broadcast(hb);
                }
                _lastHeartbeatSent = DateTime.Now;
            }


            // Ping Logic
            if (DateTime.Now - _lastPingSent > PingInterval)
            {
               long timestamp = DateTime.Now.Ticks;
               var ping = NetworkProtocol.CreatePing(timestamp);
               if (_isClient) _transport.SendToConnectedHost(ping);
               else Broadcast(ping);
               _lastPingSent = DateTime.Now;
            }

            // Timeout Logic
            // If Host: Check all clients
            // If Client: Check Host (but we don't know Host Endpoint explicitly here easily without UdpTransport exposure)
            // Actually UdpTransport knows host. But NetworkController receives packets from Host.
            // We need to track Host Endpoint in NetworkController if we are client.
            // When we receive ANY packet from Host (or anyone if client only talks to host), we update timestamp.
            
            // For now, let's just check ConnectedClients (Host Logic).
            // Client Timeout Logic: If we haven't received anything for > 5s?
            
            var now = DateTime.Now;
            List<IPEndPoint> timedOut = new List<IPEndPoint>();
            
            foreach(var kvp in _lastDataReceived)
            {
                if (now - kvp.Value > TimeoutThreshold)
                {
                    timedOut.Add(kvp.Key);
                }
            }

            foreach(var ep in timedOut)
            {
                 Console.WriteLine($"[Network] Timeout: {ep}");
                 RemoveClient(ep);
                 OnDisconnected?.Invoke(ep, "Timed Out");
            }
        }

        public void Close()
        {
            if (_isClient)
            {
                try { _transport.SendToConnectedHost(NetworkProtocol.CreateDisconnect("Quit")); } catch {}
            }
            else
            {
                // Notify clients?
                Broadcast(NetworkProtocol.CreateDisconnect("Host Quit"));
            }
            _transport.Dispose();
        }

        public void SendJoinRequest()
        {
            _transport.SendToConnectedHost(NetworkProtocol.CreateJoinRequest());
        }

        public void SendWelcome(IPEndPoint target, int assignedId, int seed, int totalPlayers)
        {
            _transport.SendTo(NetworkProtocol.CreateWelcome(assignedId, seed, totalPlayers), target);
        }

        public void BroadcastLobbyUpdate(int connectedCount, int totalPlayers, int slotMask)
        {
            Broadcast(NetworkProtocol.CreateLobbyUpdate(connectedCount, totalPlayers, slotMask));
        }

        public void BroadcastStartGame(int seed, int totalPlayers)
        {
            Broadcast(NetworkProtocol.CreateStartGame(seed, totalPlayers));
        }

        public void BroadcastDiscoveryRequest(int startPort, int endPort)
        {
            var packet = NetworkProtocol.CreateDiscoveryRequest();
            for (int p = startPort; p < endPort; p++)
            {
                // Note: ITransport doesn't expose BroadcastToPort directly anymore to keep it simple.
                // We can construct the endpoint manually if we know it's UDP.
                // But abstraction-wise, maybe ITransport SHOULD handle broadcast logic?
                // For now, I'll assume Udp behavior: 255.255.255.255
                
                // Let's use SendTo with Broadcast IP.
                _transport.SendTo(packet, new IPEndPoint(IPAddress.Broadcast, p));
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

        public void SendStateSync(IPEndPoint target, byte[] snapshot)
        {
            // Chunking
            const int CHUNK_SIZE = 1000; // Safe UDP payload size
            int totalChunks = (int)Math.Ceiling(snapshot.Length / (double)CHUNK_SIZE);
            
            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * CHUNK_SIZE;
                int len = Math.Min(CHUNK_SIZE, snapshot.Length - offset);
                
                byte[] chunkData = new byte[len];
                Array.Copy(snapshot, offset, chunkData, 0, len);
                
                byte[] packet = NetworkProtocol.CreateStateChunk(i, totalChunks, chunkData);
                _transport.SendTo(packet, target);
                
                // Small delay to prevent UDP buffer overflow on sender/receiver?
                System.Threading.Thread.Sleep(2); 
            }
            Console.WriteLine($"[Network] Sent StateSync to {target} in {totalChunks} chunks.");
        }

        public void SendDisconnect(IPEndPoint target, string reason)
        {
            byte[] packet = NetworkProtocol.CreateDisconnect(reason);
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
                // Host broadcasting input to all clients
                Broadcast(packet);
            }
            else 
            {
                // Client sending input to Host
                _transport.SendToConnectedHost(packet);
            }
        }

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

             // Update Timestamp
            _lastDataReceived[sender] = DateTime.Now;
            
            // If we are client, we should probably track the sender as "Server" if not already?
            // UdpTransport handles the socket connection.

            PacketType type = (PacketType)data[0];

            switch (type)
            {
                case PacketType.Heartbeat:
                    // Just timestamp update (done above)
                    break;
                
                case PacketType.Disconnect:
                    string reason = NetworkProtocol.ReadDisconnect(data);
                    Console.WriteLine($"[Network] Received Disconnect from {sender}: {reason}");
                    
                    // If we are a client, we must respect the disconnect from the server even if not physically in the list yet
                    // (e.g. rejection during connection phase)
                    bool wasRemoved = RemoveClient(sender);
                    
                    if (wasRemoved || _isClient)
                    {
                        OnDisconnected?.Invoke(sender, reason);
                    }
                    else
                    {
                        Console.WriteLine($"[Network] Ignoring Disconnect from unknown client {sender}");
                    }
                    break;

                case PacketType.DiscoveryRequest:
                    OnDiscoveryRequestReceived?.Invoke(sender, "Request", 0, 0); 
                    break;

                case PacketType.DiscoveryResponse:
                    var (name, cur, max) = NetworkProtocol.ReadDiscoveryResponse(data);
                    OnDiscoveryResponseReceived?.Invoke(sender, name, cur, max);
                    break;

                case PacketType.JoinRequest:
                    int version = NetworkProtocol.ReadJoinRequest(data);
                    if (version != NetworkProtocol.ProtocolVersion)
                    {
                         // Send Reject?
                        Console.WriteLine($"[Network] Rejected JoinRequest from {sender}: Protocol Version Mismatch (Incoming: {version}, Local: {NetworkProtocol.ProtocolVersion})");
                        return;
                    }
                    OnJoinRequestRaw?.Invoke(sender);
                    break;
                    
                case PacketType.StateChunk:
                    var (chunkIndex, totalChunks, chunkData) = NetworkProtocol.ReadStateChunk(data);
                    
                    if (!_reassemblyBuffers.ContainsKey(sender))
                    {
                        _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
                    }

                    var buffer = _reassemblyBuffers[sender];
                    // Validation: If TotalChunks changed, maybe reset?
                    if (buffer.TotalChunks != totalChunks)
                    {
                        // Stale or new sync started? Reset.
                         _reassemblyBuffers[sender] = (new Dictionary<int, byte[]>(), totalChunks);
                         buffer = _reassemblyBuffers[sender];
                    }

                    if (!buffer.Chunks.ContainsKey(chunkIndex))
                    {
                        buffer.Chunks[chunkIndex] = chunkData;
                    }

                    // Check Completion
                    if (buffer.Chunks.Count == totalChunks)
                    {
                        Console.WriteLine($"[Network] StateSync Reassembled ({totalChunks} chunks) from {sender}");
                        
                        // Merge
                        // We need to know total size. Iterate to sum length.
                        int totalBytes = 0;
                        for(int i=0; i<totalChunks; i++) totalBytes += buffer.Chunks[i].Length;
                        
                        byte[] fullSnapshot = new byte[totalBytes];
                        int offset = 0;
                        for(int i=0; i<totalChunks; i++)
                        {
                            byte[] c = buffer.Chunks[i];
                            Array.Copy(c, 0, fullSnapshot, offset, c.Length);
                            offset += c.Length;
                        }
                        
                        _reassemblyBuffers.Remove(sender);
                        OnStateSyncReceived?.Invoke(fullSnapshot);
                    }
                    break;
                    
                case PacketType.StateSync:
                    // Legacy or single-packet fallback
                    byte[] snap = NetworkProtocol.ReadStateSync(data);
                    OnStateSyncReceived?.Invoke(snap);
                    break;

                case PacketType.Welcome:
                    var (assignedId, seed, total) = NetworkProtocol.ReadWelcome(data);
                    OnWelcomeReceived?.Invoke(assignedId, seed, total);
                    // If client, maybe we should explicitly track Host Endpoint here if needed?
                    break;

                case PacketType.LobbyUpdate:
                    var (connected, totalP, mask) = NetworkProtocol.ReadLobbyUpdate(data);
                    OnLobbyUpdateReceived?.Invoke(connected, totalP, mask);
                    break;

                case PacketType.StartGame:
                    var (gameSeed, gameTotal) = NetworkProtocol.ReadStartGame(data);
                    OnStartGameReceived?.Invoke(gameSeed, gameTotal);
                    break;

                case PacketType.Input:
                    var (pid, frame, inputs, remotePos, remoteHash) = NetworkProtocol.ReadInputPacket(data);
                    OnInputReceived?.Invoke(pid, frame, inputs, remotePos, remoteHash);
                    break;

                case PacketType.LobbyReady:
                    var (readyPid, isReady) = NetworkProtocol.ReadLobbyReady(data);
                    OnLobbyReadyReceived?.Invoke(readyPid, isReady);
                    break;

                case PacketType.Ping:
                    long pTimestamp = NetworkProtocol.ReadPing(data);
                    var pong = NetworkProtocol.CreatePong(pTimestamp);
                    _transport.SendTo(pong, sender);
                    break;

                case PacketType.Pong:
                    long pontTimestamp = NetworkProtocol.ReadPong(data);
                    long rttTicks = DateTime.Now.Ticks - pontTimestamp;
                    LastPingMs = (int)(rttTicks / TimeSpan.TicksPerMillisecond);
                    // Optional: Smooth this value
                    break;


            }
        }

        public void SendLobbyReady(int pid, bool isReady)
        {
             var packet = NetworkProtocol.CreateLobbyReady(pid, isReady);
             if (_isClient) _transport.SendToConnectedHost(packet);
             else Broadcast(packet);
        }

        public void BroadcastLobbyReady(int pid, bool isReady)
        {
             // Host broadcasts to all
             Broadcast(NetworkProtocol.CreateLobbyReady(pid, isReady));
        }

        public void SendLobbyReadyTo(IPEndPoint target, int pid, bool isReady)
        {
            _transport.SendTo(NetworkProtocol.CreateLobbyReady(pid, isReady), target);
        }


    }
}
