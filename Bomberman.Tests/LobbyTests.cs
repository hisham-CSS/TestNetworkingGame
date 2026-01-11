using System;
using System.Threading;
using Bomberman.Net;
using NUnit.Framework;

namespace Bomberman.Tests
{
    [TestFixture]
    public class LobbyTests
    {
        [Test]
        public void ReadyStatus_PropagatesFromClientToHost()
        {
            // Setup
            int hostPort = 6000;
            var host = new NetworkController(hostPort);
            var client = new NetworkController(0);

            bool hostReceivedReady = false;
            int readyPid = -1;
            bool readyValue = false;

            host.OnLobbyReadyReceived += (pid, val) => 
            {
                hostReceivedReady = true;
                readyPid = pid;
                readyValue = val;
            };

            // Act
            // Client simulates sending ready for Player 1
            // Note: In real app, Client sends Ready, Host receives, Host broadcasts.
            // Here we test Client -> Host
            
            client.Connect("127.0.0.1", hostPort);
            
            // Give time to connect
            for(int i=0; i<10; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            // Client sends "I am ready" (PacketType.LobbyReady)
            // But wait, SendLobbyReady sends to Host? Yes.
            // We need to fake the PID assignment first?
            // Actually SendLobbyReady sends {PacketType.LobbyReady, PID, IsReady}
            // Client needs to know its PID. Assume PID=1.
            
            client.SendLobbyReady(1, true);

            // Tick
            for(int i=0; i<10; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            // Assert
            Assert.That(hostReceivedReady, Is.True, "Host failed to receive Ready packet");
            Assert.That(readyPid, Is.EqualTo(1));
            Assert.That(readyValue, Is.True);
            
            host.Close();
            client.Close();
        }

        [Test]
        public void HostBroadcast_UpdatesClientReadyStatus()
        {
            // Setup
            int hostPort = 6001;
            var host = new NetworkController(hostPort);
            var client = new NetworkController(0);

            bool clientReceivedUpdate = false;
            int updatedPid = -1;
            bool updatedValue = false;
            
            // Host logic: Add client when JoinRequest received
            host.OnJoinRequestRaw += (sender) => 
            {
                host.AddClient(sender);
                // In real app, we'd send Welcome, but for this test we strictly need the client in the list for Broadcast to work.
            };

            client.OnLobbyReadyReceived += (pid, val) => 
            {
                clientReceivedUpdate = true;
                updatedPid = pid;
                updatedValue = val;
            };

            client.Connect("127.0.0.1", hostPort);
            client.SendJoinRequest();
            
            // Wait for connection (JoinRequest -> Host AddClient)
            for(int i=0; i<50; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            // Act
            // Host sends "Player 0 is Ready" to all
            host.BroadcastLobbyReady(0, true);

            // Tick
            for(int i=0; i<50; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            // Assert
            Assert.That(clientReceivedUpdate, Is.True, "Client failed to receive Ready broadcast");
            Assert.That(updatedPid, Is.EqualTo(0));
            Assert.That(updatedValue, Is.True);

            host.Close();
            client.Close();
        }

        [Test]
        public void GracefulDisconnect_TriggersEvent()
        {
            // Setup
            int hostPort = 6002;
            var host = new NetworkController(hostPort);
            var client = new NetworkController(0);

            bool hostDetectedDisconnect = false;
            string disconnectReason = "";

            host.OnDisconnected += (sender, reason) => 
            {
                hostDetectedDisconnect = true;
                disconnectReason = reason;
            };

            // Connect
            client.Connect("127.0.0.1", hostPort);
            client.SendJoinRequest();

            // Host must accept the client for Disconnect to be valid
            host.OnJoinRequestRaw += (sender) => host.AddClient(sender);
            
            for(int i=0; i<50; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            // Act - Client Closes Gracefully
            // This sends PacketType.Disconnect "Quit"
            client.Close();

            // Tick Host to process the packet
            for(int i=0; i<50; i++) { host.Update(); Thread.Sleep(10); }

            // Assert
            Assert.That(hostDetectedDisconnect, Is.True, "Host failed to detect client disconnect");
            Assert.That(disconnectReason, Is.EqualTo("Quit"));
            
            host.Close();
        }
    }
}
