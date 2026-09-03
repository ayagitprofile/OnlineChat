using System.Net.Sockets;

namespace Server;

public record ClientConnection(TcpClient Client, string Name)
{
    public override string ToString() => $"({Name}, {Client.Client.RemoteEndPoint})";
}