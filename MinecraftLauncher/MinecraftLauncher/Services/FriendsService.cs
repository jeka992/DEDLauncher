using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

namespace DedLauncher.Services;

/// <summary>
/// Друзья, чат, заявки, приглашения и групповые чаты через публичный MQTT-брокер.
/// Темы:
///   dedlauncher/v1/{pairhash}  — личный канал пары друзей (presence/chat/invite)
///   dedlauncher/v1/req/{code}  — заявки конкретному пользователю
///   dedlauncher/v1/grp/{code}  — групповой чат по коду группы
/// </summary>
public class FriendsService : IDisposable
{
    private readonly IMqttClient _client;
    private readonly Dictionary<string, string> _friendsByTopic = new(); // pair-topic -> code
    private readonly HashSet<string> _groupCodes = new();
    private bool _started;

    public string MyCode { get; }
    public string DisplayName { get; set; } = "";

    public event Action<string, string, string?, string?, bool>? PresenceReceived; // code, name, server, status, online
    public event Action<string, string, string>? MessageReceived;                  // code, name, text
    public event Action<string>? TypingReceived;                                    // code
    public event Action<string, string>? RequestReceived;                          // code, name
    public event Action<string, string>? RequestAccepted;                          // code, name
    public event Action<string, string>? InviteReceived;                           // code, server
    public event Action<string, string>? GroupPresence;                            // groupCode, name
    public event Action<string, string, string>? GroupMessage;                     // groupCode, name, text
    public event Action<string, string, string?, string?>? SkinReceived;           // code, mcName, skinBase64?, capeBase64?
    public event Action<string>? SkinRequested;                                     // code (друг просит наш скин)

    public event Action? Connected;

    public FriendsService(string myCode, string brokerHost = "broker.hivemq.com", int port = 1883)
    {
        MyCode = myCode;
        _client = new MqttClientFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
        _client.ConnectedAsync += async e =>
        {
            await ResubscribeAllAsync();
            Connected?.Invoke();
        };
        _client.DisconnectedAsync += async e =>
        {
            await Task.Delay(3000);
            await ConnectAsync(brokerHost, port);
        };
        BrokerHost = brokerHost;
        BrokerPort = port;
    }

    public string BrokerHost { get; }
    public int BrokerPort { get; }

    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;
        await ConnectAsync(BrokerHost, BrokerPort);
    }

    private async Task ConnectAsync(string host, int port)
    {
        try
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId("ded-" + MyCode + "-" + Random.Shared.Next(100000, 999999))
                .WithCleanSession()
                .Build();
            await _client.ConnectAsync(options, CancellationToken.None);
        }
        catch { }
    }

    // ─── Темы ───

    private static string PairTopic(string a, string b)
    {
        var pair = new[] { a, b }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair[0] + "|" + pair[1]))).ToLower();
        return "dedlauncher/v1/" + hash;
    }

    private static string ReqTopic(string code) => "dedlauncher/v1/req/" + code.ToUpper();
    private static string GroupTopic(string code) => "dedlauncher/v1/grp/" + code.ToUpper();

    // ─── Подписки ───

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
        await SubscribeAsync(ReqTopic(MyCode));
        foreach (var topic in _friendsByTopic.Keys.ToList())
            await SubscribeAsync(topic);
        foreach (var g in _groupCodes.ToList())
            await SubscribeAsync(GroupTopic(g));
    }

    // ─── Друзья и заявки ───

    public async Task AddFriendAsync(string code)
    {
        var topic = PairTopic(MyCode, code);
        _friendsByTopic[topic] = code;
        await SubscribeAsync(topic);
        await PublishPresenceAsync();
    }

    public async Task SendRequestAsync(string code)
    {
        await PublishAsync(ReqTopic(code), new { t = "req", from = MyCode, name = DisplayName });
    }

    public async Task AcceptRequestAsync(string code)
    {
        await AddFriendAsync(code);
        await PublishAsync(ReqTopic(code), new { t = "req_accept", from = MyCode, name = DisplayName });
        await PublishPresenceAsync();
    }

    public void RemoveFriend(string code)
    {
        var topic = PairTopic(MyCode, code);
        if (_friendsByTopic.Remove(topic))
            _ = _client.UnsubscribeAsync(topic);
    }

    public async Task InviteToServerAsync(string code, string server)
    {
        await PublishAsync(PairTopic(MyCode, code), new { t = "invite", from = MyCode, server });
    }

    // ─── Скины и плащи (DED-пользователи видят их друг у друга) ───

    /// <summary>Отправляет другу свой скин/плащ (base64 PNG).</summary>
    public async Task PublishSkinAsync(string code, string mcName, string? skinBase64, string? capeBase64)
    {
        await PublishAsync(PairTopic(MyCode, code), new
        {
            t = "sk",
            from = MyCode,
            name = DisplayName,
            mcname = mcName,
            skin = skinBase64 ?? "",
            cape = capeBase64 ?? ""
        });
    }

    /// <summary>Просим друга прислать его скин/плащ.</summary>
    public async Task RequestSkinAsync(string code)
    {
        await PublishAsync(PairTopic(MyCode, code), new { t = "sk_req", from = MyCode });
    }

    public async Task PublishPresenceAsync(string? server = null, string? status = null)
    {
        if (!_client.IsConnected) return;
        var payload = JsonSerializer.Serialize(new
        {
            t = "p",
            from = MyCode,
            name = DisplayName,
            server = server ?? "",
            status = status ?? "",
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        foreach (var topic in _friendsByTopic.Keys.ToList())
        {
            try { await PublishAsync(topic, payload); } catch { }
        }
    }

    public async Task SendMessageAsync(string code, string text)
    {
        if (!_client.IsConnected) return;
        var payload = JsonSerializer.Serialize(new
        {
            t = "c",
            from = MyCode,
            name = DisplayName,
            text,
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        try { await PublishAsync(PairTopic(MyCode, code), payload); } catch { }
    }

    /// <summary>Сигнал «печатает...» выбранному другу.</summary>
    public async Task SendTypingAsync(string code)
    {
        if (!_client.IsConnected) return;
        var payload = JsonSerializer.Serialize(new { t = "tp", from = MyCode });
        try { await PublishAsync(PairTopic(MyCode, code), payload); } catch { }
    }

    // ─── Группы ───

    public async Task JoinGroupAsync(string code)
    {
        _groupCodes.Add(code);
        await SubscribeAsync(GroupTopic(code));
        await PublishAsync(GroupTopic(code), new { t = "gp", from = MyCode, name = DisplayName });
    }

    public void LeaveGroup(string code)
    {
        if (_groupCodes.Remove(code))
            _ = _client.UnsubscribeAsync(GroupTopic(code));
    }

    public async Task SendGroupMessageAsync(string code, string text)
    {
        if (!_client.IsConnected) return;
        var payload = JsonSerializer.Serialize(new
        {
            t = "gc",
            from = MyCode,
            name = DisplayName,
            text,
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        try { await PublishAsync(GroupTopic(code), payload); } catch { }
    }

    private async Task PublishAsync(string topic, object payload)
    {
        await PublishAsync(topic, JsonSerializer.Serialize(payload));
    }

    private async Task PublishAsync(string topic, string payload)
    {
        if (!_client.IsConnected) return;
        try
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();
            await _client.PublishAsync(msg);
        }
        catch { }
    }

    // ─── Приём ───

    private Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.Payload;
        var bytes = payload.IsSingleSegment ? payload.First.ToArray() : payload.ToArray();
        var json = Encoding.UTF8.GetString(bytes);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var from = root.GetProperty("from").GetString() ?? "";
            if (from == MyCode) return Task.CompletedTask;
            var name = root.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
            var type = root.TryGetProperty("t", out var t) ? t.GetString() : "";

            // Личный канал
            if (_friendsByTopic.TryGetValue(topic, out var code))
            {
                if (type == "p")
                {
                    var server = root.TryGetProperty("server", out var s) ? s.GetString() : "";
                    var status = root.TryGetProperty("status", out var st) ? st.GetString() : "";
                    PresenceReceived?.Invoke(code, name, string.IsNullOrEmpty(server) ? null : server,
                        string.IsNullOrEmpty(status) ? null : status, true);
                }
                else if (type == "c")
                {
                    var text = root.TryGetProperty("text", out var tx) ? tx.GetString() : "";
                    if (!string.IsNullOrEmpty(text))
                        MessageReceived?.Invoke(code, name, text);
                }
                else if (type == "tp")
                {
                    TypingReceived?.Invoke(code);
                }
                else if (type == "invite")
                {
                    var server = root.TryGetProperty("server", out var s) ? s.GetString() : "";
                    InviteReceived?.Invoke(code, server);
                }
                else if (type == "sk")
                {
                    var mcname = root.TryGetProperty("mcname", out var mn) ? mn.GetString() : name;
                    var skin = root.TryGetProperty("skin", out var sk) ? sk.GetString() : "";
                    var cape = root.TryGetProperty("cape", out var cp) ? cp.GetString() : "";
                    SkinReceived?.Invoke(code, mcname,
                        string.IsNullOrEmpty(skin) ? null : skin,
                        string.IsNullOrEmpty(cape) ? null : cape);
                }
                else if (type == "sk_req")
                {
                    SkinRequested?.Invoke(code);
                }
                return Task.CompletedTask;
            }

            // Заявки
            if (topic == ReqTopic(MyCode))
            {
                if (type == "req") RequestReceived?.Invoke(from, name);
                else if (type == "req_accept") RequestAccepted?.Invoke(from, name);
                return Task.CompletedTask;
            }

            // Группы
            var groupTopicPrefix = "dedlauncher/v1/grp/";
            if (topic.StartsWith(groupTopicPrefix))
            {
                var groupCode = topic.Substring(groupTopicPrefix.Length);
                if (type == "gp") GroupPresence?.Invoke(groupCode, name);
                else if (type == "gc")
                {
                    var text = root.TryGetProperty("text", out var tx) ? tx.GetString() : "";
                    if (!string.IsNullOrEmpty(text)) GroupMessage?.Invoke(groupCode, name, text);
                }
            }
        }
        catch { }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _client.Dispose(); } catch { }
    }
}
