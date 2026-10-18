using MQCtrl.Models;
using MQCtrl.Platforms;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace MQCtrl.Services;

public sealed record IncomingMqttMessage(string Topic, string Payload, DateTime Time);

public sealed class MqttConnectionService : IAsyncDisposable
{
    private readonly MqttFactory _factory = new();
    private IMqttClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _hasConnected;
    private readonly object _gate = new();

    public event Action<string>? StatusChanged;
    public event Action<IncomingMqttMessage>? MessageReceived;
    public event Action<string>? Log;
    public bool IsConnected => _client?.IsConnected == true;

    public async Task StartAsync(MqttSettings settings, IEnumerable<string> topics)
    {
        await StopAsync();
        settings = MqttPlatformAdapterFactory.Get(settings.Platform).Prepare(settings);
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.ClientId))
        {
            StatusChanged?.Invoke("待配置");
            return;
        }
        var distinctTopics = topics.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (distinctTopics.Length == 0)
        {
            StatusChanged?.Invoke("无启用主题");
            return;
        }

        var cts = new CancellationTokenSource();
        var client = _factory.CreateMqttClient();
        lock (_gate) { _cts = cts; _client = client; _loop = RunLoopAsync(client, settings, distinctTopics, cts.Token); }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            _client = null;
            _loop = null;
        }
        cts?.Cancel();
        StatusChanged?.Invoke("已断开");
    }

    private async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            _cts = null;
            _client = null;
            _loop = null;
        }
        cts?.Cancel();
        if (loop is not null)
            try { await loop; } catch { }
        cts?.Dispose();
    }

    private async Task RunLoopAsync(IMqttClient client, MqttSettings settings, string[] topics, CancellationToken token)
    {
        client.ApplicationMessageReceivedAsync += e =>
        {
            var payload = e.ApplicationMessage.ConvertPayloadToString() ?? "";
            MessageReceived?.Invoke(new(e.ApplicationMessage.Topic, payload, DateTime.Now));
            return Task.CompletedTask;
        };
        var attempt = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    StatusChanged?.Invoke(_hasConnected || attempt > 0 ? "重连中" : "连接中");
                    var options = new MqttClientOptionsBuilder()
                        .WithClientId(settings.ClientId.Trim())
                        .WithTcpServer(settings.Host.Trim(), settings.Port)
                        .WithCleanSession()
                        .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));
                    if (!string.IsNullOrEmpty(settings.Username) || !string.IsNullOrEmpty(settings.Password))
                        options.WithCredentials(settings.Username, settings.Password);
                    if (settings.UseTls) options.WithTlsOptions(o => o.UseTls());

                    await client.ConnectAsync(options.Build(), token);
                    attempt = 0;
                    StatusChanged?.Invoke("在线");
                    var reconnecting = _hasConnected;
                    Log?.Invoke(reconnecting
                        ? $"MQTT 重新连接：{settings.Host}:{settings.Port}"
                        : $"MQTT 已连接：{settings.Host}:{settings.Port}");
                    foreach (var topic in topics)
                        await client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), token);
                    Log?.Invoke(reconnecting
                        ? $"重新连接后已重新订阅 {topics.Length} 个主题"
                        : $"已订阅 {topics.Length} 个主题");
                    _hasConnected = true;

                    while (client.IsConnected && !token.IsCancellationRequested)
                        await Task.Delay(1000, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke("连接失败");
                    Log?.Invoke($"MQTT 错误：{ex.Message}");
                    attempt++;
                }

                try { await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Max(2, attempt * 2))), token); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally { client.Dispose(); }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
