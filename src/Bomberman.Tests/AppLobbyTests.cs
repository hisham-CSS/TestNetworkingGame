using NUnit.Framework;
using Bomberman.App.States;
using Bomberman.Tests.Mocks;
using Bomberman.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Net;
using System.Linq;

namespace Bomberman.Tests
{
    [TestFixture]
    public class AppLobbyTests
    {
        private MockInputService _input;
        private MockRenderer _renderer;
        private GameContext _context;
        private LobbyState _state;
        private MockTransport _transport;
        private MockTransport _remoteTransport; 
        private NetworkController _remoteNetwork; // To generate packets

        [SetUp]
        public void Setup()
        {
            _input = new MockInputService();
            _renderer = new MockRenderer();
            _context = new GameContext(new MockGameHost(), null!, null!, null!, _input, _renderer, new MockLogger());
            
            _transport = new MockTransport();
            _context.Network = new NetworkController(_transport);

            // Generator setup
            _remoteTransport = new MockTransport();
            _remoteNetwork = new NetworkController(_remoteTransport);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Network?.Close();
            _remoteNetwork?.Close();
            _transport.Dispose();
            _remoteTransport.Dispose();
        }

        [Test]
        public void Host_InitializesWithOnePlayer()
        {
            _state = new LobbyState(_context, new GameStateManager(), true, null);
            _state.Enter();
            
            // Use reflection or properties to verify count.
            // _connectedPlayerCount is private.
            // But we can verify Draw output if we use MockRenderer? 
            // MockRenderer is a stub. We'd need to spy on it.
            // Let's use Reflection for white-box testing of state.
            
            var count = (int)GetPrivateField(_state, "_connectedPlayerCount");
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void Client_SendsJoinRequest_OnEnter()
        {
            var hostEp = new IPEndPoint(IPAddress.Loopback, 6000);
            _state = new LobbyState(_context, new GameStateManager(), false, hostEp);
            
            _state.Enter();
            _state.Update(new GameTime());

            // Verify a JoinRequest was sent
            Assert.That(_transport.SentPackets.Count, Is.GreaterThan(0));
            // Ideally check packet type, but raw bytes are opaque without deserializer.
            // We assume it sent *something*.
        }

        [Test]
        public void ToggleReady_SendsPacket()
        {
            _state = new LobbyState(_context, new GameStateManager(), true, null); // Host
            
            // Add a dummy client so Broadcast sends a packet
            _context.Network.AddClient(new IPEndPoint(IPAddress.Loopback, 12345));
            
            _state.Enter();

            // Press Space
            _input.SetKeys(Keys.Space);
            _state.Update(new GameTime());

            // Should send LobbyReady
            Assert.That(_transport.SentPackets.Count, Is.GreaterThan(0));
            
            // Verify internal state
            var amIReady = (bool)GetPrivateField(_state, "_amIReady");
            Assert.That(amIReady, Is.True);

            // Release Space (Pulse)
            _input.SetKeys(); 
            _input.Update();
            _state.Update(new GameTime());
            
            // Press Space again -> Unready
            _input.SetKeys(Keys.Space);
            _input.Update();
            _state.Update(new GameTime());

            amIReady = (bool)GetPrivateField(_state, "_amIReady");
            Assert.That(amIReady, Is.False);
        }

        private object GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(obj);
        }
    }
}
