using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MQCtrl.Models;
using System.IO;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace MQCtrl;

public partial class FeatureDialog : Window
{
    private readonly bool _editing;
    public CommandFeature Result { get; private set; } = new();
    public bool DeleteRequested { get; private set; }

    public FeatureDialog(CommandFeature? feature = null)
    {
        InitializeComponent();
        _editing = feature is not null;
        if (feature is not null)
        {
            TitleText.Text = "编辑指令";
            DeleteButton.Visibility = Visibility.Visible;
            NameBox.Text = feature.Name;
            TopicBox.Text = feature.Topic;
            MatchBox.Text = feature.MatchValue;
            ScriptBox.Text = feature.ScriptPath;
            EnabledBox.IsChecked = feature.Enabled;
            ActionBox.SelectedIndex = feature.Action switch { "restart" => 1, "lock" => 2, "script" => 3, _ => 0 };
            Result = feature;
        }
        else
        {
            ActionBox.SelectedIndex = 0;
            MatchBox.Text = "on";
            EnabledBox.IsChecked = true;
        }
        UpdateScriptVisibility();
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e) => Close();

    private string SelectedAction => (ActionBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "shutdown";
    private void ActionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateScriptVisibility();
    private void UpdateScriptVisibility() => ScriptPanel.Visibility = SelectedAction == "script" ? Visibility.Visible : Visibility.Collapsed;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择要运行的脚本", Filter = "脚本文件 (*.ps1;*.bat;*.cmd)|*.ps1;*.bat;*.cmd|所有文件 (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) ScriptBox.Text = dialog.FileName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(TopicBox.Text) || string.IsNullOrWhiteSpace(MatchBox.Text))
        {
            MessageBox.Show(this, "名称、主题和匹配消息都不能为空。", "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var action = SelectedAction;
        if (action == "script" && (string.IsNullOrWhiteSpace(ScriptBox.Text) || !File.Exists(ScriptBox.Text)))
        {
            MessageBox.Show(this, "请选择一个存在的脚本文件。", "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result.Name = NameBox.Text.Trim();
        Result.Topic = TopicBox.Text.Trim();
        Result.MatchValue = MatchBox.Text;
        Result.Action = action;
        Result.ScriptPath = ScriptBox.Text.Trim();
        Result.Enabled = EnabledBox.IsChecked == true;
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"确定删除“{Result.Name}”吗？", "删除指令", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            DeleteRequested = true;
            DialogResult = true;
        }
    }
}
