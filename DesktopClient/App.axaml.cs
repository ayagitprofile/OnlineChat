using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace DesktopClient;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loginWindow = new LoginWindow(async (loginWindow, name, ip, port) =>
                {
                    loginWindow.ConnectButtonEnabled = false;

                    var client = new TcpClient();
                    try
                    {
                        await client.ConnectAsync(ip.ToString(), port);
                    }
                    catch (SocketException e)
                    {
                        loginWindow.ShowError($"Failed to connect, why: {e.Message}");
                        loginWindow.ConnectButtonEnabled = true;
                        return;
                    }

                    await Protocol.Protocol.WriteAsync
                    (
                        client.GetStream(),
                        new Protocol.Protocol.OutgoingData
                        (
                            Protocol.Protocol.DataType.UserNameUTF8,
                            Encoding.UTF8.GetBytes(name)
                        ),
                        CancellationToken.None
                    );

                    desktop.MainWindow = new MainWindow(client);
                    desktop.MainWindow.Show();
                    loginWindow.Hide();
                });

            desktop.MainWindow = loginWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
