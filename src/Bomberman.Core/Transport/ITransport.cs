namespace Bomberman.Core;

public interface ITransport
{
    void Send(byte[] data); // Sends to Host (Client usage) or specific target?
    // UdpTransport.Send sends to _remoteEndPoint (Host).
    
    void Broadcast(byte[] data); // Sends to all connected clients (Host usage).
}
