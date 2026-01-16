using System;
using System.Net;
using Bomberman.Net.Handlers;
using NUnit.Framework;

namespace Bomberman.Tests.Net.Handlers
{
    [TestFixture]
    public class PacketReassemblerTests
    {
        [Test]
        public void HandleChunk_SingleChunk_CompletesImmediately()
        {
            var reassembler = new PacketReassembler();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var data = new byte[] { 1, 2, 3 };
            bool complete = false;
            byte[] result = null;

            reassembler.HandleChunk(endpoint, 0, 1, data, res => {
                complete = true;
                result = res;
            });

            Assert.That(complete, Is.True);
            Assert.That(result, Is.EqualTo(data));
        }

        [Test]
        public void HandleChunk_MultipleChunks_InOrder_Completes()
        {
            var reassembler = new PacketReassembler();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var data1 = new byte[] { 1, 2 };
            var data2 = new byte[] { 3, 4 };
            bool complete = false;
            byte[] result = null;

            reassembler.HandleChunk(endpoint, 0, 2, data1, res => complete = true);
            Assert.That(complete, Is.False);

            reassembler.HandleChunk(endpoint, 1, 2, data2, res => {
                complete = true;
                result = res;
            });

            Assert.That(complete, Is.True);
            Assert.That(result, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void HandleChunk_MultipleChunks_OutOfOrder_Completes()
        {
            var reassembler = new PacketReassembler();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var data1 = new byte[] { 1, 2 };
            var data2 = new byte[] { 3, 4 };
            bool complete = false;
            byte[] result = null;

            // Send chunk 1 first (index 1)
            reassembler.HandleChunk(endpoint, 1, 2, data2, res => complete = true);
            Assert.That(complete, Is.False);

            // Send chunk 0 second (index 0)
            reassembler.HandleChunk(endpoint, 0, 2, data1, res => {
                complete = true;
                result = res;
            });

            Assert.That(complete, Is.True);
            Assert.That(result, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void HandleChunk_NewSequence_ResetsBuffer()
        {
            var reassembler = new PacketReassembler();
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var data1 = new byte[] { 1 };
            
            // Start a 2-chunk sequence
            reassembler.HandleChunk(endpoint, 0, 2, data1, res => { });
            
            // Start a new 3-chunk sequence (should reset previous)
            bool complete = false;
            var dataNew1 = new byte[] { 10 };
            var dataNew2 = new byte[] { 11 };
            var dataNew3 = new byte[] { 12 };

            reassembler.HandleChunk(endpoint, 0, 3, dataNew1, res => complete = true); // 1/3
            reassembler.HandleChunk(endpoint, 1, 3, dataNew2, res => complete = true); // 2/3
            Assert.That(complete, Is.False);
            
            byte[] result = null;
            reassembler.HandleChunk(endpoint, 2, 3, dataNew3, res => {
                complete = true;
                result = res;
            }); // 3/3

            Assert.That(complete, Is.True);
            Assert.That(result, Is.EqualTo(new byte[] { 10, 11, 12 }));
        }
    }
}
