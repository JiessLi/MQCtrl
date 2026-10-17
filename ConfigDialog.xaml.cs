using System.Windows;
using MQCtrl.Models;
using MessageBox = System.Windows.MessageBox;

namespace MQCtrl;

public partial class ConfigDialog : Window
{
    public MqttSettings Result { get; private set; }

    public ConfigDialog(MqttSettings settings)
    {
        InitializeComponent();
        Result = new MqttSettings { Platform = settings.Platform, Host = settings.Host, Port = settings.Port, ClientId = settings.ClientId, Username = settings.Username, Password = settings.Password, UseTls = settings.UseTls };
        PlatformBox.SelectedIndex = settings.Platform == "Generic" ? 1 : 0;
        HostBox.Text = settings.Host;
        PortBox.Text = settings.Port.ToString();
        ClientIdBox.Password = settings.ClientId;
        UsernameBox.Text = settings.Username;
        PasswordBox.Password = settings.Password;
        TlsBox.IsChecked = settings.UseTls;
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Close();

    private void CopyClientId_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ClientIdBox.Password)) return;
        try
        {
            System.Windows.Clipboard.SetText(ClientIdBox.Password);
            if (sender is FrameworkElement element) element.ToolTip = "已复制密钥";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            MessageBox.Show(this, "无法访问剪贴板，请稍后重试。", "MQCtrl", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show(this, "请输入有效的端口号（1–65535）。", "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(HostBox.Text) || string.IsNullOrWhiteSpace(ClientIdBox.Password))
        {
            MessageBox.Show(this, "服务器地址和客户端 ID 不能为空。", "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = new MqttSettings
        {
            Platform = PlatformBox.SelectedIndex == 1 ? "Generic" : "Bemfa",
            Host = HostBox.Text.Trim(), Port = port, ClientId = ClientIdBox.Password.Trim(),
            Username = UsernameBox.Text.Trim(), Password = PasswordBox.Password, UseTls = TlsBox.IsChecked == true
        };
        DialogResult = true;
    }

    private void TlsBox_Changed(object sender, RoutedEventArgs e)
    {
        if (PortBox is null || !int.TryParse(PortBox.Text, out var port)) return;
        if (TlsBox.IsChecked == true && port == 9501) PortBox.Text = "9503";
        else if (TlsBox.IsChecked != true && port == 9503) PortBox.Text = "9501";
    }
}
