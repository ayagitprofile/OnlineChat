using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls.ApplicationLifetimes;
using System.Text.Json;

namespace DesktopClient;

public partial class MainWindow : Window
{
    private TcpClient _clientConnection;

    private CancellationTokenSource _messageRecieverCancellationTokenSource = new();

    private Task _messageReceiverTask;

    public MainWindow(TcpClient clientConnection)
    {
        _clientConnection = clientConnection;

        _messageReceiverTask = RecieveMessagesAsync(() =>
        {
            ConnectionStatus.Foreground = new SolidColorBrush(Color.Parse("#b70000"));
            ConnectionStatus.Text = "Server disconnected";
            Console.WriteLine("Server disconnected");
        }, _messageRecieverCancellationTokenSource.Token);

        InitializeComponent();
    }

    private async Task RecieveMessagesAsync(Action handleServerDisconnecting, CancellationToken cancellationToken)
    {
        var stream = _clientConnection.GetStream();

        try
        {
            while (await Protocol.Protocol.ReadAsync(stream, cancellationToken) is Protocol.Protocol.ReceivedData receivedData)
            {
                switch (receivedData.Type)
                {
                    case Protocol.Protocol.DataType.ChatMessageJson:
                        {
                            if (JsonSerializer.Deserialize<Protocol.Protocol.ChatMessage>(receivedData.Data) is Protocol.Protocol.ChatMessage message)
                            {
                                AddMessageSentByOtherUsers(message.SenderName, message.Text, message.Timestamp);
                            }
                            else
                            {
                                AddMessageSentByOtherUsers("Unknown", "Failed to deserialize message sent by server", DateTimeOffset.Now);
                            }
                        }
                        break;
                    default:
                        {
                            AddMessageSentByOtherUsers("Unknown", "Server sent unsupported message type", DateTimeOffset.Now);
                        }
                        break;
                }
            }
            handleServerDisconnecting();
        }
        catch (IOException)
        {
            handleServerDisconnecting();
        }
        catch (OperationCanceledException)
        {

        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Console.WriteLine("Window closed");

        _messageRecieverCancellationTokenSource.Cancel();

        _messageReceiverTask.Wait();

        _clientConnection.Close();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't remove focus when clicking the input itself.
        if (e.Source is Control control && (control == MessageInput))
        {
            return;
        }

        Focus();
    }

    private void SendButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        SendMessage();
    }

    private void MessageInput_KeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        SendMessage();

        e.Handled = true;
    }

    private void SendMessage()
    {
        var text = MessageInput.Text;

        if (string.IsNullOrWhiteSpace(text))
            return;

        AddMessageSentByUser(text, DateTimeOffset.Now);

        _ = Task.Run(async () =>
        {
            var data
                = new Protocol.Protocol.OutgoingData
                (
                    Protocol.Protocol.DataType.MessageTextUTF8,
                    Encoding.UTF8.GetBytes(text)
                );

            await Protocol.Protocol.WriteAsync(_clientConnection.GetStream(), data, CancellationToken.None);
        });

        MessageInput.Text = string.Empty;
        MessageInput.Focus();
    }

    private void AddMessageSentByOtherUsers(string name, string text, DateTimeOffset timestamp)
        => AddMessage(name, text, timestamp, false);

    private void AddMessageSentByUser(string text, DateTimeOffset timestamp)
        => AddMessage("You", text, timestamp, true);

    private void AddMessage(
        string sender,
        string text,
        DateTimeOffset timestamp,
        bool ownMessage)
    {
        var container = new StackPanel
        {
            HorizontalAlignment = ownMessage
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left,

            MaxWidth = 500
        };

        // Sender name
        if (!ownMessage)
        {
            container.Children.Add(
                new TextBlock
                {
                    Text = sender,
                    Foreground = new SolidColorBrush(
                        Color.Parse("#AAAAAA")),

                    FontSize = 12,

                    Margin = new Thickness(4, 0, 0, 4)
                });
        }

        // Message bubble
        var bubble = new Border
        {
            Background = ownMessage
                ? new SolidColorBrush(
                    Color.Parse("#2563EB"))
                : new SolidColorBrush(
                    Color.Parse("#303030")),

            CornerRadius = new CornerRadius(10),

            Padding = new Thickness(12, 8),

            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            }
        };

        container.Children.Add(bubble);

        // Timestamp
        container.Children.Add(
            new TextBlock
            {
                Text = $"{timestamp.Hour}:{timestamp.Minute}",

                Foreground = new SolidColorBrush(
                    Color.Parse("#888888")),

                FontSize = 11,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin = new Thickness(4, 3, 4, 0)
            });

        MessagesPanel.Children.Add(container);

        MessageScrollViewer.ScrollToEnd();
    }
}
