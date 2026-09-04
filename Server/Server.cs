using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using static Protocol.Protocol;

namespace Server;

record struct BroadcastedMessage(ClientConnection Sender, string Message);

public sealed class Server(int port) : IAsyncDisposable
{
    private ConcurrentDictionary<Guid, ClientConnection> _clientConnections = new();
    private ConcurrentBag<Task> _concurrentTasks = [];

    private CancellationTokenSource _cancellationTokenSource = new();

    private Channel<BroadcastedMessage> _broadcastedMessages = Channel.CreateUnbounded<BroadcastedMessage>();

    public async Task Run()
    {
        using var http = new HttpClient();

        var publicIp = await http.GetStringAsync("https://api.ipify.org");

        var listener = new TcpListener(IPAddress.Any, port);

        listener.Start();

        Console.WriteLine($"[Server] Started\n[Server] Public IP: {publicIp}\n[Server] Local IP: {IPAddress.Loopback}\n[Server] Listening on port: {port}");

        try
        {
            _concurrentTasks.Add(BroadcastMessagesAsync(_cancellationTokenSource.Token));

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();

                _ = AcceptClientAsync(client, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {

        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task AcceptClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        if (await PerformClientHandshakeAsync(client) is ClientConnection connection)
        {
            var id = Guid.NewGuid();
            if (_clientConnections.TryAdd(id, connection))
            {
                Console.WriteLine($"[Server] Client accepted: {connection}");
                _concurrentTasks.Add(HandleClientConnection(id, cancellationToken));
            }
        }
    }

    private async Task BroadcastMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _broadcastedMessages.Reader.WaitToReadAsync(cancellationToken))
            {
                if (_broadcastedMessages.Reader.TryRead(out var message))
                {
                    foreach (var client in _clientConnections)
                    {
                        if (client.Value == message.Sender)
                        {
                            continue;
                        }

                        var json = JsonSerializer.SerializeToUtf8Bytes(new ChatMessage(message.Sender.Name, message.Message, DateTimeOffset.Now));

                        await WriteAsync
                        (
                            client.Value.Client.GetStream(),
                            new OutgoingData(DataType.ChatMessageJson, json),
                            cancellationToken
                        );
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {

        }
    }

    private async Task HandleClientConnection(Guid connectionID, CancellationToken cancellationToken)
    {
        if (!_clientConnections.TryGetValue(connectionID, out var connection))
        {
            return;
        }

        var stream = connection.Client.GetStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested && await ReadAsync(stream, cancellationToken) is ReceivedData receivedData)
            {
                switch (receivedData.Type)
                {
                    case DataType.MessageTextUTF8:
                        {
                            var message = Encoding.UTF8.GetString(receivedData.Data);
                            Console.WriteLine($"[Server] Client {connection} sent message: {message}");
                            await _broadcastedMessages.Writer.WriteAsync(new BroadcastedMessage(connection, message), cancellationToken);
                        }
                        break;
                    case DataType.UserNameUTF8:
                        {
                            Console.WriteLine($"[Server] Client {connection} tried to send name data after initial handshake, it's not allowed");
                        }
                        break;
                }
            }

            Console.WriteLine($"[Server] Client ({connection.Name}) disconnected");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Server] Client ({connection.Name}) disconnected unexpectedly, why: {e.Message}");
        }
        finally
        {
            connection.Client.Close();
            _clientConnections.TryRemove(connectionID, out _);
        }
    }

    private static async Task<ClientConnection?> PerformClientHandshakeAsync(TcpClient client)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            var stream = client.GetStream();

            if (await ReadAsync(stream, timeout.Token) is ReceivedData receivedData)
            {
                switch (receivedData.Type)
                {
                    case DataType.UserNameUTF8:
                        {
                            var name = Encoding.UTF8.GetString(receivedData.Data);
                            return new ClientConnection(client, name);
                        }
                    default:
                        {
                            Console.WriteLine($"[Server] Failed to accept client connection, client must send its name first before sending other data");
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            Console.WriteLine($"[Server] Failed to accept client connection, client ({client.Client.RemoteEndPoint}) havent sent its name");
        }

        client.Close();
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[Server] Shutting down");
        _cancellationTokenSource.Cancel();
        await Task.WhenAll(_concurrentTasks);
        _cancellationTokenSource.Dispose();
    }
}