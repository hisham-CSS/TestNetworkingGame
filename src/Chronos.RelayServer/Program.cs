using System;
using System.Net;
using System.Net.Sockets;
using Chronos.Net.Protocol;

namespace Chronos.RelayServer
{
    public class Program
    {
        private static UdpClient? _udpServer;
        // Session ID -> List of Clients
        private static Dictionary<ushort, List<IPEndPoint>> _sessions = new();
        // Client Endpoint -> Session ID (for quick lookup/cleanup)
        private static Dictionary<IPEndPoint, ushort> _clientSessions = new();
        
        private const int Port = 7777;

        public static async Task Main(string[] args)
        {
            Console.WriteLine($"[Chronos Relay] Starting on port {Port}...");
            
            try 
            {
                _udpServer = new UdpClient(Port);
                // Windows specific: Fix for UDP ConnectionReset error on some clients disconnecting
                if (System.OperatingSystem.IsWindows())
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    _udpServer.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Fatal] Failed to bind to port {Port}: {ex.Message}");
                return;
            }

            Console.WriteLine("[Chronos Relay] Ready to accept connections.");

            while (true)
            {
                try
                {
                    var result = await _udpServer.ReceiveAsync();
                    ProcessPacket(result.Buffer, result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Receive Loop: {ex.Message}");
                }
            }
        }

        private static void ProcessPacket(byte[] data, IPEndPoint sender)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var header = RelayHeader.Deserialize(reader);

                switch (header.PacketType)
                {
                    case RelayPacketType.JoinSession:
                        HandleJoin(header, sender);
                        break;
                    case RelayPacketType.LeaveSession:
                        HandleLeave(sender);
                        break;
                    case RelayPacketType.RelayPacket:
                        HandleRelay(header, data, sender);
                        break;
                }
            }
            catch (Exception)
            {
                // Malformed packet, ignore
            }
        }

        private static void HandleJoin(RelayHeader header, IPEndPoint sender)
        {
            if (!_sessions.ContainsKey(header.SessionId))
            {
                _sessions[header.SessionId] = new List<IPEndPoint>();
                Console.WriteLine($"[Session {header.SessionId}] Created.");
            }

            var sessionClients = _sessions[header.SessionId];
            
            // Check if already in session
            if (!sessionClients.Contains(sender))
            {
                // If this client was in another session, remove them first
                if (_clientSessions.ContainsKey(sender))
                {
                    HandleLeave(sender);
                }

                sessionClients.Add(sender);
                _clientSessions[sender] = header.SessionId;
                Console.WriteLine($"[Session {header.SessionId}] Client {sender} Joined (PlayerId: {header.SourcePlayerId}). Total: {sessionClients.Count}");

                // Send Ack back to client so they know they are connected
                SendAck(sender, header.SessionId, header.SourcePlayerId);
            }
        }

        private static void SendAck(IPEndPoint target, ushort sessionId, byte playerId)
        {
             var header = new RelayHeader 
             { 
                 PacketType = RelayPacketType.JoinSessionAck,
                 SessionId = sessionId,
                 SourcePlayerId = playerId
             };
             
             using var ms = new MemoryStream();
             using var writer = new BinaryWriter(ms);
             header.Serialize(writer);
             byte[] pkt = ms.ToArray();
             _udpServer?.SendAsync(pkt, pkt.Length, target);
        }

        private static void HandleLeave(IPEndPoint sender)
        {
            if (_clientSessions.TryGetValue(sender, out ushort sessionId))
            {
                if (_sessions.TryGetValue(sessionId, out var sessionClients))
                {
                    sessionClients.Remove(sender);
                    Console.WriteLine($"[Session {sessionId}] Client {sender} Left. Remaining: {sessionClients.Count}");
                    
                    if (sessionClients.Count == 0)
                    {
                        _sessions.Remove(sessionId);
                        Console.WriteLine($"[Session {sessionId}] Closed (Empty).");
                    }
                }
                _clientSessions.Remove(sender);
            }
        }

        private static void HandleRelay(RelayHeader header, byte[] fullPacket, IPEndPoint sender)
        {
            // Verify sender is actually in the session they claim
            if (_clientSessions.TryGetValue(sender, out ushort registeredSessionId))
            {
                // Security check: Don't allow spoofing session ID
                if (registeredSessionId != header.SessionId) return;

                if (_sessions.TryGetValue(header.SessionId, out var targets))
                {
                    foreach (var target in targets)
                    {
                        // Don't echo back to sender
                        if (!target.Equals(sender)) 
                        {
                            _udpServer?.SendAsync(fullPacket, fullPacket.Length, target);
                        }
                    }
                }
            }
        }
    }
}
