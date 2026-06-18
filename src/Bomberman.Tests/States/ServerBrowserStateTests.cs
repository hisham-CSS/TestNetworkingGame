using NUnit.Framework;
using Bomberman.App.States;
using Bomberman.Tests.Mocks;
using Chronos.Net;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Moq;
using Microsoft.Xna.Framework;
using System.Net;
using System.Linq;
using System.Collections.Generic;

using Bomberman.App.Rendering;
using Bomberman.App.GameHost;

namespace Bomberman.Tests.States
{
    [TestFixture]
    public class ServerBrowserStateTests
    {
        private Mock<IInputService> _input;
        private Mock<IRenderer> _renderer;
        private GameContext _context;
        private ServerBrowserState _state = null!;
        
        // Transport Mocks
        private Mock<ITransport> _transportMock;
        private List<(byte[] Data, IPEndPoint Endpoint)> _sentPackets;

        // Helper to generate packets
        private Mock<ITransport> _generatorTransportMock;
        private List<(byte[] Data, IPEndPoint Endpoint)> _generatorSentPackets;
        private NetworkController<InputState> _generatorNetwork;

        [SetUp]
        public void Setup()
        {
            _input = new Mock<IInputService>();
            _renderer = new Mock<IRenderer>();
            
            var mockGame = new Mock<IGameHost>();
            mockGame.Setup(g => g.WindowWidth).Returns(800);
            mockGame.Setup(g => g.WindowHeight).Returns(600);

            _context = new GameContext(mockGame.Object, null!, null!, null!, _input.Object, _renderer.Object, new Mock<ILogger>().Object);
            
            // Generator Setup
            _generatorTransportMock = new Mock<ITransport>();
            _generatorSentPackets = new List<(byte[] Data, IPEndPoint Endpoint)>();
            _generatorTransportMock.Setup(x => x.SendTo(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()))
                 .Callback<byte[], IPEndPoint>((data, ep) => _generatorSentPackets.Add((data, ep)));

            _generatorNetwork = new NetworkController<InputState>(_generatorTransportMock.Object);

            // Context Transport Setup
            _transportMock = new Mock<ITransport>();
            _sentPackets = new List<(byte[] Data, IPEndPoint Endpoint)>();
             _transportMock.Setup(x => x.SendTo(It.IsAny<byte[]>(), It.IsAny<IPEndPoint>()))
                 .Callback<byte[], IPEndPoint>((data, ep) => _sentPackets.Add((data, ep)));

            _context.Network = new NetworkController<InputState>(_transportMock.Object);
            
            _state = new ServerBrowserState(_context, new GameStateManager()); 
        }
        
        [TearDown]
        public void Teardown()
        {
            _context.Network?.Close();
            _generatorNetwork?.Close();
        }

        [Test]
        public void Enter_BroadcastsDiscovery()
        {
            _state.Enter();
            // Should send Broadcast
            Assert.That(_sentPackets.Count, Is.GreaterThan(0));
            // Check broadcasting range? Maybe later.
        }

        [Test]
        public void ReceiveDiscoveryResponse_AddsServerToList()
        {
            _state.Enter();

            // 1. Generate Response Packet
            var serverEp = new IPEndPoint(IPAddress.Loopback, 1234);
            _generatorNetwork.SendDiscoveryResponse(serverEp, "Test Server", 2, 4); 
            
            // 2. Extract bytes from generator
            var packet = _generatorSentPackets[0]; 
            // The generator thinks it sent it to 'serverEp', but we just want the data.
            
            // 3. Inject into Browser's Transport
            // Browser expects 'OnDiscoveryResponseReceived' event to fire.
            // NetworkController parses 'DiscoveryResponse' packet.
            
            _transportMock.Raise(x => x.PacketReceived += null, packet.Data, serverEp);
            
            // 4. Update Network to process packet
            _context.Network!.Update();
            
            // 5. Check State
            var list = (System.Collections.IList)GetPrivateField(_state, "_servers");
            Assert.That(list.Count, Is.EqualTo(1));
            
            // Reflection to check server details?
            dynamic serverInfo = list[0]!;
            Assert.That(serverInfo.Name, Is.EqualTo("Test Server"));
        }

        private object GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(obj)!;
        }
    }
}
