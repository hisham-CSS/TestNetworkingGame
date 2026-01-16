using NUnit.Framework;
using Bomberman.App.States;
using Bomberman.Tests.Mocks;
using Bomberman.Net;
using Bomberman.App.Input;
using Bomberman.Core.Input;
using Bomberman.Core.Logging;
using Moq;
using Microsoft.Xna.Framework;
using System.Net;
using System.Linq;
using System.Collections.Generic;

namespace Bomberman.Tests
{
    [TestFixture]
    public class ServerBrowserStateTests
    {
        private Mock<IInputService> _input;
        private MockRenderer _renderer;
        private GameContext _context;
        private ServerBrowserState _state;
        private MockTransport _transport;
        
        // Helper to generate packets
        private MockTransport _generatorTransport;
        private NetworkController _generatorNetwork;

        [SetUp]
        public void Setup()
        {
            _input = new Mock<IInputService>();
            _renderer = new MockRenderer();
            _context = new GameContext(new MockGameHost(), null!, null!, null!, _input.Object, _renderer, new Mock<ILogger>().Object);
            
            _generatorTransport = new MockTransport();
            _generatorNetwork = new NetworkController(_generatorTransport);

            _transport = new MockTransport();
            _context.Network = new NetworkController(_transport);
            
            _state = new ServerBrowserState(_context, new GameStateManager()); 
        }
        
        [TearDown]
        public void Teardown()
        {
            _context.Network?.Close();
            _generatorNetwork?.Close();
            _transport.Dispose();
            _generatorTransport.Dispose();
        }

        [Test]
        public void Enter_BroadcastsDiscovery()
        {
            _state.Enter();
            // Should send Broadcast
            Assert.That(_transport.SentPackets.Count, Is.GreaterThan(0));
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
            var packet = _generatorTransport.SentPackets.Dequeue(); 
            // The generator thinks it sent it to 'serverEp', but we just want the data.
            
            // 3. Inject into Browser's Transport
            // Browser expects 'OnDiscoveryResponseReceived' event to fire.
            // NetworkController parses 'DiscoveryResponse' packet.
            
            _transport.SimulateReceive(packet.Data, serverEp);
            
            // 4. Update Network to process packet
            _context.Network.Update();
            
            // 5. Check State
            var list = (System.Collections.IList)GetPrivateField(_state, "_servers");
            Assert.That(list.Count, Is.EqualTo(1));
            
            // Reflection to check server details?
            dynamic serverInfo = list[0];
            Assert.That(serverInfo.Name, Is.EqualTo("Test Server"));
        }

        private object GetPrivateField(object obj, string name)
        {
            return obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(obj);
        }
    }
}
