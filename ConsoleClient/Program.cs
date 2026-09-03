using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using static Protocol.Protocol;

const int PORT = 5000;

var incomingMessages = Channel.CreateUnbounded<string>();
var outgoingMessages = Channel.CreateUnbounded<string>();

Console.WriteLine("Online app console client started\ntype /q, to exit");

Console.WriteLine($"Connectiong to server: ({System.Net.IPAddress.Loopback}, {PORT})");

var serverConnection = new TcpClient(System.Net.IPAddress.Loopback.ToString(), PORT);

Console.WriteLine("Connected to server successfully, sending name");

await WriteAsync(serverConnection.GetStream(), new OutgoingData(DataType.UserNameUTF8, Encoding.UTF8.GetBytes("Alex")), CancellationToken.None);

Console.WriteLine("Name sent successfully");

var cancellationTokenSource = new CancellationTokenSource();

var consoleInputTask = Task.Run(ConsoleInput, cancellationTokenSource.Token);

var messageSenderTask = SendMessagesAsync(cancellationTokenSource.Token);

var messageReceiverTask = ReceiveMessagesAsync(cancellationTokenSource.Token);

var consoleOutputTask = ConsoleOutputAsync(cancellationTokenSource.Token);

await consoleInputTask;

cancellationTokenSource.Cancel();

await Task.WhenAll(messageSenderTask, messageReceiverTask, consoleOutputTask);

serverConnection.Close();

async Task SendMessagesAsync(CancellationToken cancellationToken)
{
    var stream = serverConnection.GetStream();

    try
    {
        while (await outgoingMessages.Reader.WaitToReadAsync(cancellationToken))
        {
            while (outgoingMessages.Reader.TryRead(out var message))
            {
                await WriteAsync(stream, new OutgoingData(DataType.MessageTextUTF8, Encoding.UTF8.GetBytes(message)), cancellationToken);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
}

async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
{
    var stream = serverConnection.GetStream();

    try
    {
        while (await ReadAsync(stream, cancellationToken) is ReceivedData receivedData)
        {
            if (receivedData.Type == DataType.MessageTextUTF8)
            {
                await incomingMessages.Writer.WriteAsync(Encoding.UTF8.GetString(receivedData.Data), cancellationToken);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
}

async Task ConsoleOutputAsync(CancellationToken cancellationToken)
{
    try
    {
        while (await incomingMessages.Reader.WaitToReadAsync(cancellationToken))
        {
            while (incomingMessages.Reader.TryRead(out var message))
            {
                Console.WriteLine(message);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
}

void ConsoleInput()
{
    while (Console.ReadLine() is string line)
    {
        if (line == "/q")
        {
            return;
        }

        outgoingMessages.Writer.TryWrite(line);
    }
}