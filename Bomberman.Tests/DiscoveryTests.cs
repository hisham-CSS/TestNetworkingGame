using System;
using System.Net;
using System.Threading;
using Bomberman.Net;
using NUnit.Framework;

namespace Bomberman.Tests
{
    [TestFixture]
    public class DiscoveryTests
    {
        [Test]
        public void TestDiscoveryFlow()
        {
            // Setup Host
            int hostPort = 5500;
            var host = new NetworkController(hostPort);
            
            // Setup Client (Browser)
            var client = new NetworkController(0); // Random port
            
            bool discoveryRequestReceived = false;
            bool discoveryResponseReceived = false;
            
            // Host Logic
            host.OnDiscoveryRequestReceived += (sender, header, cur, max) => 
            {
                discoveryRequestReceived = true;
                host.SendDiscoveryResponse(sender, "Test Server", 1, 4);
            };

            // Client Logic
            client.OnDiscoveryResponseReceived += (sender, name, cur, max) =>
            {
                discoveryResponseReceived = true;
            };

            // Act
            // Client Broadcasts to port range including host
            client.BroadcastDiscoveryRequest(hostPort, hostPort + 1);

            // Tick a few times to process sockets
            for(int i=0; i<20; i++)
            {
                host.Update();
                client.Update();
                Thread.Sleep(50);
            }

            // Assert
            // Assert
            Assert.That(discoveryRequestReceived, Is.True, "Host did not receive discovery request");
            Assert.That(discoveryResponseReceived, Is.True, "Client did not receive discovery response");
            
            host.Close();
            client.Close();
        }
    }
}
