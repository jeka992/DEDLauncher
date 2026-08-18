using System.Buffers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MQTTnet;
using MQTTnet.Protocol;
using DedLauncher.Helpers;

namespace DedLauncher.Services;

/// <summary>
/// Модуль подчинения: лаунчер слушает команды мод-приложения DEDAdmin
/// и шлёт статусы через публичный MQTT-брокер.
///
/// Темы (namespace dedadm/v1):
///   st/{deviceId}   — статус игрока (админ подписан на st/+)
///   all             — анонсы для всех (retained)
///   one/{deviceId}  — персональные команды (запрос диагностики)
///   banlist         — бан-лист (retained)
///   diag/{deviceId} — ответы диагностики
/// </summary>
public class AdminClientService : IDisposable
{
    private const string Prefix = "dedadm/v1/";
    private const string BrokerHost = "broker.hivemq.com";
    private const int BrokerPort = 1883;

    private readonly IMqttClient _client;
    private readonly System.Threading.Timer _heartbeat;
    private List<BanEntry> _bans = new();

    /// <summary>Уникальный ID копии лаунчера (генерируется один раз).</summary>
    public string DeviceId { get; }

    public string Nick { get; set; } = "";
    public string AccountType { get; set; } = "offline";
    public string McVersion { get; set; } = "";
    public string Loader { get; set; } = "";
    public string GameState { get; set; } = "online"; // online | playing
    public string Server { get; set; } = "";

    public event Action<string, string>? AnnouncementReceived; // id, text

    public AdminClientService()
    {
        DeviceId = LoadOrCreateDeviceId();
        _client = new MqttClientFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
        _client.ConnectedAsync += async e =>
        {
            await ResubscribeAllAsync();
            await PublishStatusAsync();
        };
        _client.DisconnectedAsync += async e =>
        {
            await Task.Delay(5000);
            await ConnectAsync();
        };
        _heartbeat = new System.Threading.Timer(_ => _ = PublishStatusAsync(), null,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    private static string LoadOrCreateDeviceId()
    {
        try
        {
            var path = Path.Combine(MinecraftPathHelper.BaseDir, "device.json");
            if (File.Exists(path))
            {
                var id = JsonSerializer.Deserialize<DeviceFile>(File.ReadAllText(path))?.DeviceId;
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
            var guid = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, JsonSerializer.Serialize(new DeviceFile { DeviceId = guid }));
            return guid;
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private class DeviceFile
    {
        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = "";
    }

    public async Task StartAsync()
    {
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        try
        {
            var will = JsonSerializer.Serialize(new
            {
                t = "st", d = DeviceId, nick = Nick, state = "offline",
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(BrokerHost, BrokerPort)
                .WithClientId("dedadm-" + DeviceId[..Math.Min(12, DeviceId.Length)] + "-" + Random.Shared.Next(100000, 999999))
                .WithCleanSession()
                .WithWillTopic(Prefix + "st/" + DeviceId)
                .WithWillPayload(will)
                .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            await _client.ConnectAsync(options, CancellationToken.None);
        }
        catch { }
    }

    private async Task SubscribeAsync(string topic)
    {
        try
        {
            var options = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic, MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            await _client.SubscribeAsync(options);
        }
        catch { }
    }

    private async Task ResubscribeAllAsync()
    {
        await SubscribeAsync(Prefix + "all");
        await SubscribeAsync(Prefix + "banlist");
        await SubscribeAsync(Prefix + "ping");
        await SubscribeAsync(Prefix + "one/" + DeviceId);
    }

    /// <summary>Публикует текущий статус копии лаунчера.</summary>
    public async Task PublishStatusAsync(string? state = null, string? server = null)
    {
        if (state != null) GameState = state;
        if (server != null) Server = server;
        if (!_client.IsConnected) return;
        var payload = JsonSerializer.Serialize(new
        {
            t = "st",
            d = DeviceId,
            nick = Nick,
            acc = AccountType,
            mc = McVersion,
            loader = Loader,
            state = GameState,
            server = Server,
            os = Environment.OSVersion.VersionString,
            ram = SystemInfo.TotalRamMb,
            ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "",
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        try
        {
            await _client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(Prefix + "st/" + DeviceId)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build());
        }
        catch { }
    }

    /// <summary>Забанена ли эта копия лаунчера (по последнему известному бан-листу).</summary>
    public bool IsBanned(out string reason, out DateTime? until)
    {
        reason = "";
        until = null;
        try
        {
            var ban = _bans.FirstOrDefault(b => b.D == DeviceId);
            if (ban == null) return false;
            if (ban.Until > 0)
            {
                var u = DateTimeOffset.FromUnixTimeSeconds(ban.Until).LocalDateTime;
                if (u <= DateTime.Now) return false; // срок вышел
                until = u;
            }
            reason = ban.Reason;
            return true;
        }
        catch { return false; }
    }

    /// <summary>Отвечает на запрос диагностики админа.</summary>
    private async Task RespondDiagnosticsAsync(string reqId)
    {
        try
        {
            var modsDir = Path.Combine(MinecraftPathHelper.GameDir, "mods");
            var mods = Directory.Exists(modsDir)
                ? Directory.GetFiles(modsDir, "*.jar").Select(Path.GetFileName).Take(100).ToList()
                : new List<string?>();

            var logTail = new List<string>();
            try
            {
                var logPath = Path.Combine(MinecraftPathHelper.BaseDir, "launcher.log");
                if (File.Exists(logPath))
                    logTail = File.ReadLines(logPath).Reverse().Take(15).Reverse().ToList();
            }
            catch { }

            var payload = JsonSerializer.Serialize(new
            {
                t = "diagresp",
                id = reqId,
                d = DeviceId,
                nick = Nick,
                mc = McVersion,
                loader = Loader,
                mods,
                log = logTail,
                os = Environment.OSVersion.VersionString,
                ram = SystemInfo.TotalRamMb,
                ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? ""
            });
            await _client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(Prefix + "diag/" + DeviceId)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build());
        }
        catch { }
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.Payload;
        var bytes = payload.IsSingleSegment ? payload.First.ToArray() : payload.ToArray();
        var json = Encoding.UTF8.GetString(bytes);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("t", out var t) ? t.GetString() : "";

            if (topic == Prefix + "banlist" && type == "ban")
            {
                var list = new List<BanEntry>();
                if (root.TryGetProperty("list", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        list.Add(new BanEntry
                        {
                            D = el.TryGetProperty("d", out var d) ? d.GetString() ?? "" : "",
                            Nick = el.TryGetProperty("nick", out var n) ? n.GetString() ?? "" : "",
                            Until = el.TryGetProperty("until", out var u) ? u.GetInt64() : 0,
                            Reason = el.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : ""
                        });
                    }
                }
                _bans = list;
            }
            else if (topic == Prefix + "all" && type == "ann")
            {
                var id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                var text = root.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(text) && !IsAnnouncementSeen(id))
                {
                    MarkAnnouncementSeen(id);
                    AnnouncementReceived?.Invoke(id, text);
                }
            }
            else if (topic == Prefix + "one/" + DeviceId && type == "diag")
            {
                var id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(id))
                    await RespondDiagnosticsAsync(id);
            }
            else if (topic == Prefix + "ping" && type == "ping")
            {
                await PublishStatusAsync();
            }
        }
        catch { }
        await Task.CompletedTask;
    }

    private static string SeenAnnouncePath =>
        Path.Combine(MinecraftPathHelper.BaseDir, "lastannounce.txt");

    private static bool IsAnnouncementSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        try { return File.Exists(SeenAnnouncePath) && File.ReadAllText(SeenAnnouncePath).Trim() == id; }
        catch { return false; }
    }

    private static void MarkAnnouncementSeen(string id)
    {
        try { File.WriteAllText(SeenAnnouncePath, id); } catch { }
    }

    private class BanEntry
    {
        public string D { get; set; } = "";
        public string Nick { get; set; } = "";
        public long Until { get; set; }
        public string Reason { get; set; } = "";
    }

    public void Dispose()
    {
        try { _heartbeat.Dispose(); } catch { }
        try { _client.Dispose(); } catch { }
    }
}
