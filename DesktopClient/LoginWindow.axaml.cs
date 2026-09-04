using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace DesktopClient;

using UserLoginIsDoneCallback = Action<LoginWindow, string, IPAddress, int>;

public partial class LoginWindow : Window
{
    private UserLoginIsDoneCallback _userLoginIsDoneCallback;

    public bool ConnectButtonEnabled { get => ConnectButton.IsEnabled; set => ConnectButton.IsEnabled = value; }

    public LoginWindow(UserLoginIsDoneCallback callback)
    {
        _userLoginIsDoneCallback = callback;

        InitializeComponent();

        NameInput.Focus();
    }

    private void ConnectButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Connect();
    }

    private void Connect()
    {
        ErrorText.Text = string.Empty;

        string name = NameInput.Text?.Trim() ?? string.Empty;
        string ip = IpInput.Text?.Trim() ?? string.Empty;
        string portText = PortInput.Text?.Trim() ?? string.Empty;

        // Validate name
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Please enter your name.");
            NameInput.Focus();
            return;
        }

        // Validate IP
        if (!IPAddress.TryParse(ip, out var ipAddress))
        {
            ShowError("Please enter a valid IP address.");
            IpInput.Focus();
            return;
        }

        // Validate port
        if (!int.TryParse(portText, out int port) ||
            port < 1 ||
            port > 65535)
        {
            ShowError("Please enter a valid port between 1 and 65535.");
            PortInput.Focus();
            return;
        }

        // Data is valid.
        _userLoginIsDoneCallback.Invoke(this, name, IPAddress.Parse(ip), port);
    }

    public void ShowError(string message)
    {
        ErrorText.Text = message;
    }
}