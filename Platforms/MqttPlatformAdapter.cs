using MQCtrl.Models;

namespace MQCtrl.Platforms;

public interface IMqttPlatformAdapter
{
    string Id { get; }
    MqttSettings Prepare(MqttSettings settings);
}

public sealed class BemfaMqttAdapter : IMqttPlatformAdapter
{
    public string Id => "Bemfa";
    public MqttSettings Prepare(MqttSettings settings)
    {
        var result = Copy(settings);
        if (string.IsNullOrWhiteSpace(result.Host)) result.Host = "bemfa.com";
        if (result.Port < 1) result.Port = result.UseTls ? 9503 : 9501;
        return result;
    }
    private static MqttSettings Copy(MqttSettings s) => new() { Platform = s.Platform, Host = s.Host, Port = s.Port, ClientId = s.ClientId, Username = s.Username, Password = s.Password, UseTls = s.UseTls };
}

public sealed class GenericMqttAdapter : IMqttPlatformAdapter
{
    public string Id => "Generic";
    public MqttSettings Prepare(MqttSettings settings) => new() { Platform = settings.Platform, Host = settings.Host, Port = settings.Port, ClientId = settings.ClientId, Username = settings.Username, Password = settings.Password, UseTls = settings.UseTls };
}

public static class MqttPlatformAdapterFactory
{
    private static readonly IReadOnlyDictionary<string, IMqttPlatformAdapter> Adapters = new Dictionary<string, IMqttPlatformAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["Bemfa"] = new BemfaMqttAdapter(), ["Generic"] = new GenericMqttAdapter()
    };

    public static IMqttPlatformAdapter Get(string? platform) => platform is not null && Adapters.TryGetValue(platform, out var adapter) ? adapter : Adapters["Generic"];
}
