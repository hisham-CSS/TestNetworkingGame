using NUnit.Framework;
using Bomberman.App.States;
using Bomberman.Tests.Mocks;
using Chronos.Net;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Moq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Net;
using System.Linq;
using System.Collections.Generic;

using Bomberman.App.Rendering;
using Bomberman.App.GameHost;

namespace Bomberman.Tests.States
{
    [TestFixture]
    public class AppLobbyTests
    {
        private Mock<IInputService> _input;
        private Mock<IRenderer> _renderer;
        private GameContext _context;
        private LobbyState _state;
        
        // Transport Mocks
        private Mock<ITransport> _transportMock;
        private List<(byte[] Data, IPEndPoint Endpoint)> _sentPackets;

        private Mock<ITransport> _remoteTransportMock;
        private List<(byte[] Data, IPEndPoint Endpoint)> _remoteSentPackets; 
        private NetworkController<InputState> _remoteNetwork; 

        [SetUp]
        public void Setup()
        {
            _input = new Mock<IInputService>();
            _renderer = new Mock<IRenderer>();
            
            var mockGame = new Mock<IGameHost>();
            mockGame.Setup(g => g.WindowWidth).Returns(800);
            mockGame.Setup(g => g.WindowHeight).Returns(600);

            _context = new GameContext(mockGame.Object, null!, null!, null!, _input.Object, _renderer.Object, new Mock<ILogger>().Object);
            
            // Setup Main Transport
            _transportMock = new Mock<ITransport>();
            _sentPackets = new List<(byte[] Data, IPEndPoint Endpoint)>();
            _transportMock.Setup(x => x.SendTo(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], IPEndPoint>((data, ep) => _sentPackets.Add((data, ep)));
            
            // Allow capturing packets sent via SendToConnectedHost
            _transportMock.Setup(x => x.SendToConnectedHost(It.IsAny<byte[]>()))
                .Callback<byte[]>(data => _sentPackets.Add((data, null!)));

            _context.Network = new NetworkController<InputState>(_transportMock.Object);

            // Setup Remote Transport (Generator)
            _remoteTransportMock = new Mock<ITransport>();
            _remoteSentPackets = new List<(byte[] Data, IPEndPoint Endpoint)>();
            _remoteTransportMock.Setup(x => x.SendTo(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()))
                .Callback<byte[], IPEndPoint>((data, ep) => _remoteSentPackets.Add((data, ep)));
            
            _remoteNetwork = new NetworkController<InputState>(_remoteTransportMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Network?.Close();
            _remoteNetwork?.Close();
        }

        [Test]
        public void Host_InitializesWithOnePlayer()
        {
            _state = new LobbyState(_context, new GameStateManager(), true, null);
            _state.Enter();
            
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
            Assert.That(_sentPackets.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ToggleReady_SendsPacket()
        {
            _state = new LobbyState(_context, new GameStateManager(), true, null); // Host
            
            // Add a dummy client so Broadcast sends a packet
            _context.Network.AddClient(new IPEndPoint(IPAddress.Loopback, 12345));
            
            _state.Enter();

            // Press Space
            _input.Setup(x => x.GetKeyboard()).Returns(new KeyboardState(Keys.Space));
            _state.Update(new GameTime());

            // Should send LobbyReady
            Assert.That(_sentPackets.Count, Is.GreaterThan(0));
            
            // Verify internal state
            var amIReady = (bool)GetPrivateField(_state, "_amIReady");
            Assert.That(amIReady, Is.True);

            // Release Space (Pulse)
            // Clear packets to verify next action
            _sentPackets.Clear(); 
            _input.Setup(x => x.GetKeyboard()).Returns(new KeyboardState());
            _state.Update(new GameTime());
            
            // Press Space again -> Unready
            _input.Setup(x => x.GetKeyboard()).Returns(new KeyboardState(Keys.Space));
            _state.Update(new GameTime());

            amIReady = (bool)GetPrivateField(_state, "_amIReady");
            Assert.That(amIReady, Is.False);
            
            // Should have sent another packet
            Assert.That(_sentPackets.Count, Is.GreaterThan(0));
        }

        private object GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(obj);
        }
    }
}
