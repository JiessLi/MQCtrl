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
        ClientIdBox.Text = settings.ClientId;
        UsernameBox.Text = settings.Username;
        PasswordBox.Password = settings.Password;
        TlsBox.IsChecked = settings.UseTls;
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            MessageBox.Show(this, "请输入有效的端口号（1–65535）。", "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(HostBox.Text) || string.IsNullOrWhiteSpace(ClientIdBox.Text))
        {
            MessageBox.Show(this, "服务器地址和客户端 ID 不能为空。", "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = new MqttSettings
        {
            Platform = PlatformBox.SelectedIndex == 1 ? "Generic" : "Bemfa",
            Host = HostBox.Text.Trim(), Port = port, ClientId = ClientIdBox.Text.Trim(),
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
