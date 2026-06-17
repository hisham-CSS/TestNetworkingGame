using System.Threading;
using Bomberman.Core;
using Bomberman.Net;
using NUnit.Framework;

namespace Bomberman.Tests.Net
{
    /// <summary>
    /// Lobby lifecycle over a real loopback UDP transport: a client's ready toggle must reach the host,
    /// and the host's broadcast must reach the client. This exercises the live socket path, not just
    /// serialization.
    /// </summary>
    [TestFixture]
    public class LobbyTests
    {
        [Test]
        public void ReadyStatus_PropagatesFromClientToHost()
        {
            int hostPort = 6000;
            var host = new NetworkController<InputState>(new UdpTransport(hostPort));
            var client = new NetworkController<InputState>(new UdpTransport(0));

            bool hostReceivedReady = false;
            int readyPid = -1;
            bool readyValue = false;
            host.OnLobbyReadyReceived += (pid, val) => { hostReceivedReady = true; readyPid = pid; readyValue = val; };

            client.Connect("127.0.0.1", hostPort);
            for (int i = 0; i < 10; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            client.SendLobbyReady(1, true);
            for (int i = 0; i < 10; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            Assert.That(hostReceivedReady, Is.True, "Host failed to receive Ready packet");
            Assert.That(readyPid, Is.EqualTo(1));
            Assert.That(readyValue, Is.True);

            host.Close();
            client.Close();
        }

        [Test]
        public void HostBroadcast_UpdatesClientReadyStatus()
        {
            int hostPort = 6010;
            var host = new NetworkController<InputState>(new UdpTransport(hostPort));
            var client = new NetworkController<InputState>(new UdpTransport(0));

            bool clientReceived = false;
            int gotPid = -1;
            host.OnJoinRequestRaw += sender => host.AddClient(sender);
            client.OnLobbyReadyReceived += (pid, val) => { clientReceived = true; gotPid = pid; };

            client.Connect("127.0.0.1", hostPort);
            client.SendJoinRequest();
            for (int i = 0; i < 10; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            host.BroadcastLobbyReady(0, true);
            for (int i = 0; i < 10; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            Assert.That(clientReceived, Is.True, "Client failed to receive host's ready broadcast");
            Assert.That(gotPid, Is.EqualTo(0));

            host.Close();
            client.Close();
        }

        [Test]
        public void JoinRequest_ReachesHostAndAssignsWelcome()
        {
            int hostPort = 6020;
            var host = new NetworkController<InputState>(new UdpTransport(hostPort));
            var client = new NetworkController<InputState>(new UdpTransport(0));

            int assignedId = -1, seed = 0;
            host.OnJoinRequestRaw += sender => { host.AddClient(sender); host.SendWelcome(sender, 1, 4242, 2); };
            client.OnWelcomeReceived += (id, s, total) => { assignedId = id; seed = s; };

            client.Connect("127.0.0.1", hostPort);
            client.SendJoinRequest();
            for (int i = 0; i < 12; i++) { host.Update(); client.Update(); Thread.Sleep(10); }

            Assert.That(assignedId, Is.EqualTo(1));
            Assert.That(seed, Is.EqualTo(4242), "Client must adopt the host seed for determinism");

            host.Close();
            client.Close();
        }
    }
}
