using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Chronos.Net
{
    /// <summary>
    /// A decorator for ITransport that simulates network latency and packet loss.
    /// Useful for testing rollback and synchronization robustness.
    /// </summary>
    public class SimulatedLagTransport : ITransport
    {
        private readonly ITransport _inner;
        private readonly Random _random = new Random();
        
        // Simulation Parameters
        public int LatencyMs { get; set; } = 0;
        public int JitterMs { get; set; } = 0;
        public double PacketLossChance { get; set; } = 0.0;

        public event Action<byte[], IPEndPoint>? PacketReceived;

        private bool _isDisposed = false;
        private ConcurrentQueue<(byte[] Data, IPEndPoint Sender, DateTime DeliveryTime)> _incomingQueue 
            = new ConcurrentQueue<(byte[] Data, IPEndPoint Sender, DateTime DeliveryTime)>();

        public int LocalPort => _inner.LocalPort;

        public SimulatedLagTransport(ITransport inner)
        {
            _inner = inner;
            _inner.PacketReceived += OnInnerPacketReceived;
        }

        private void OnInnerPacketReceived(byte[] data, IPEndPoint sender)
        {
            if (_isDisposed) return;

            // Simulating Packet Loss on Receive (Inbound Loss)
            if (_random.NextDouble() < PacketLossChance)
            {
                // packet dropped
                return;
            }

            // Calculate Delay
            int delay = LatencyMs;
            if (JitterMs > 0)
            {
                delay += _random.Next(-JitterMs, JitterMs);
                if (delay < 0) delay = 0;
            }

            var deliveryTime = DateTime.Now.AddMilliseconds(delay);
            _incomingQueue.Enqueue((data, sender, deliveryTime));
        }

        public void Connect(string ip, int port)
        {
            _inner.Connect(ip, port);
        }

        public void SendToConnectedHost(byte[] data)
        {
            // We could also simulate outbound lag/loss here if desired.
            // For now, we only delay INCOMING packets to simplify the loop, 
            // as lag is symmetric effectively for RTT.
            _inner.SendToConnectedHost(data);
        }

        public void SendTo(byte[] data, IPEndPoint target)
        {
            _inner.SendTo(data, target);
        }

        public void Poll()
        {
            _inner.Poll();

            // Process Delayed Queue
            DateTime now = DateTime.Now;
            
            // We need to peek and dequeue only if time is ready. 
            // Since jitter can cause out-of-order delivery timestamps, strict queue ordering might enforce order.
            // Real UDP can be out of order.
            // If we strictly want to simulate out-of-order due to jitter, we should list-process.
            // But ConcurrentQueue is FIFO.
            // For simple lag, FIFO is fine. For Jitter causing reordering... 
            // Let's dump all ready packets into a list and invoke?
            // Actually, if a later packet arrives earlier due to jitter, it should be processed.
            // But we can't easily peek middle of ConcurrentQueue.
            
            // Simplified approach: Dequeue all, keep "not ready" ones? No, expensive.
            // Better approach: Just check the head. If head is not ready, we block? 
            // That enforces ordering (Wait for head). 
            // Real internet does not enforce ordering.
            // If we want reordering, we need a different structure.
            // Let's stick to FIFO + Delay for now (Ordered Latency) as a baseline.
            // If we want unordered, `PacketLoss` handles "missing" packets, 
            // but reordering requires a List we sort/scan.
            
            // Let's optimize for standard lag simulation (Bufferbloat/Distance).
            
            while (_incomingQueue.TryPeek(out var item))
            {
                if (now >= item.DeliveryTime)
                {
                    if (_incomingQueue.TryDequeue(out var result))
                    {
                        PacketReceived?.Invoke(result.Data, result.Sender);
                    }
                }
                else
                {
                    // Head not ready. In FIFO, everything behind is likely "later" or "blocked".
                    // If we want to support re-ordering, we can't stop here.
                    break;
                }
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
            _inner.PacketReceived -= OnInnerPacketReceived;
            _inner.Dispose();
        }
    }
}
