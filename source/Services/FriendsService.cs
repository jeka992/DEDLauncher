using System.Buffers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DedLauncher.Helpers;
using MQTTnet;
using MQTTnet.Protocol;

namespace DedLauncher.Services;

/// <summary>
/// Друзья, чат, заявки, приглашения и групповые чаты через MQTT-брокер.
///
/// Безопасность (E2E):
///   - у каждого пользователя пара ключей: ECDSA P-256 (подпись) + ECDH P-256 (обмен);
///   - личные сообщения шифруются AES-256-GCM общим секретом пары (ECDH) и подписываются;
///   - открытые ключи обмениваются при заявке/принятии (req/req_accept) и по запросу pk_req/pk;
///   - групповые сообщения шифруются ключом, выведенным из кода группы (SHA-256);
///   - транспорт — TLS (порт 8883).
///
/// Темы:
///   dedlauncher/v1/{pairhash}  — личный канал пары (presence/chat/invite/skin/ключи)
///   dedlauncher/v1/req/{code}  — заявки конкретному пользователю
///   dedlauncher/v1/grp/{code}  — групповой чат по коду группы
/// </summary>
public class FriendsService : IDisposable
{
    private readonly IMqttClient _client;
    private readonly Dictionary<string, string> _friendsByTopic = new(); // pair-topic -> code
    private readonly Dictionary<string, string> _friendSign = new();     // code -> signPub
    private readonly Dictionary<string, string> _friendAgree = new();    // code -> agreePub
    private readonly HashSet<string> _groupCodes = new();
    private bool _started;

    private ECDsa _mySign = null!;
    private ECDiffieHellman _myAgree = null!;
    private string _mySignPub = "";
    private string _myAgreePub = "";

    public string MyCode { get; }
    public string DisplayName { get; set; } = "";

    public event Action<string, string, string?, string?, bool>? PresenceReceived; // code, name, server, status, online
    public event Action<string, string, string>? MessageReceived;                  // code, name, text
    public event Action<string>? TypingReceived;                                    // code
    public event Action<string, string, string, string>? RequestReceived;           // code, name, signPub, agreePub
    public event Action<string, string, string, string>? RequestAccepted;           // code, name, signPub, agreePub
    public event Action<string, string>? InviteReceived;                           // code, server
    public event Action<string, string>? GroupPresence;                            // groupCode, name
    public event Action<string, string, string>? GroupMessage;                     // groupCode, name, text
    public event Action<string, string, string?, string?>? SkinReceived;           // code, mcName, skinBase64?, capeBase64?
    public event Action<string>? SkinRequested;                                     // code
    public event Action<string, string, string>? FriendKeysReceived;                // code, signPub, agreePub

    public event Action? Connected;

    public FriendsService(string myCode, string brokerHost = "broker.hivemq.com", int port = 8883)
    {
        MyCode = myCode;
        LoadOrCreateIdentity();
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
                .WithTlsOptions(o =>
                {
                    o.UseTls();
                    o.WithSslProtocols(SslProtocols.Tls12);
                })
                .Build();
            await _client.ConnectAsync(options, CancellationToken.None);
        }
        catch { }
    }

    // ─── Идентичность ───

    private void LoadOrCreateIdentity()
    {
        var path = Path.Combine(MinecraftPathHelper.BaseDir, "idkeys.json");
        try
        {
            if (File.Exists(path))
            {
                var doc = JsonSerializer.Deserialize<IdFile>(File.ReadAllText(path));
                if (doc != null && !string.IsNullOrEmpty(doc.Sign) && !string.IsNullOrEmpty(doc.Agree))
                {
                    _mySign = ECDsa.Create();
                    _mySign.ImportPkcs8PrivateKey(Convert.FromBase64String(TokenProtection.Unprotect(doc.Sign)), out _);
                    _myAgree = ECDiffieHellman.Create();
                    _myAgree.ImportPkcs8PrivateKey(Convert.FromBase64String(TokenProtection.Unprotect(doc.Agree)), out _);
                    _mySignPub = CryptoHelper.ExportSignPub(_mySign);
                    _myAgreePub = CryptoHelper.ExportAgreePub(_myAgree);
                    return;
                }
            }
        }
        catch { }

        _mySign = CryptoHelper.NewSignKey();
        _myAgree = CryptoHelper.NewAgreeKey();
        _mySignPub = CryptoHelper.ExportSignPub(_mySign);
        _myAgreePub = CryptoHelper.ExportAgreePub(_myAgree);
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new IdFile
            {
                Sign = TokenProtection.Protect(Convert.ToBase64String(_mySign.ExportPkcs8PrivateKey())),
                Agree = TokenProtection.Protect(Convert.ToBase64String(_myAgree.ExportPkcs8PrivateKey()))
            }));
        }
        catch { }
    }

    private class IdFile
    {
        public string Sign { get; set; } = "";
        public string Agree { get; set; } = "";
    }

    /// <summary>Фиксируем открытые ключи друга (из сохранённых friends.json или обмена).</summary>
    public void SetFriendKey(string code, string signPub, string agreePub)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(signPub) || string.IsNullOrEmpty(agreePub)) return;
        _friendSign[code] = signPub;
        _friendAgree[code] = agreePub;
    }

    private bool HasFriendKey(string code)
        => _friendSign.TryGetValue(code, out var s) && _friendAgree.TryGetValue(code, out var a)
           && !string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(a);

    private void StoreFriendKey(string code, string signPub, string agreePub)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(signPub) || string.IsNullOrEmpty(agreePub)) return;
        _friendSign[code] = signPub;
        _friendAgree[code] = agreePub;
        FriendKeysReceived?.Invoke(code, signPub, agreePub);
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

    // ─── Отправка: конверты E2E ───

    private string SignData(string data) => CryptoHelper.Sign(_mySign, data);

    /// <summary>Шифрует и подписывает сообщение другу.</summary>
    private async Task PublishSealedAsync(string topic, string code, string type, string innerJson)
    {
        if (!HasFriendKey(code))
        {
            await PublishKeyRequestAsync(code);
            return;
        }
        var key = CryptoHelper.Derive(_myAgree, _friendAgree[code]);
        var (nonce, cipher) = CryptoHelper.Encrypt(key, Encoding.UTF8.GetBytes(innerJson));
        var k = Convert.ToBase64String(nonce);
        var d = Convert.ToBase64String(cipher);
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = type,
            from = MyCode,
            spk = _mySignPub,
            k,
            d,
            s = SignData($"{type}|{MyCode}|{_mySignPub}|{k}|{d}")
        });
        await PublishAsync(topic, envelope);
    }

    /// <summary>Запрашиваем открытые ключи друга (миграция старых друзей без ключей).</summary>
    private async Task PublishKeyRequestAsync(string code)
    {
        var topic = PairTopic(MyCode, code);
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = "pk_req",
            from = MyCode,
            spk = _mySignPub,
            apk = _myAgreePub,
            s = SignData($"pk_req|{MyCode}|{_mySignPub}|{_myAgreePub}")
        });
        await PublishAsync(topic, envelope);
    }

    private async Task SendMyKeysAsync(string code)
    {
        var topic = PairTopic(MyCode, code);
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = "pk",
            from = MyCode,
            spk = _mySignPub,
            apk = _myAgreePub,
            s = SignData($"pk|{MyCode}|{_mySignPub}|{_myAgreePub}")
        });
        await PublishAsync(topic, envelope);
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
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = "req",
            from = MyCode,
            name = DisplayName,
            spk = _mySignPub,
            apk = _myAgreePub,
            s = SignData($"req|{MyCode}|{DisplayName}|{_mySignPub}|{_myAgreePub}")
        });
        await PublishAsync(ReqTopic(code), envelope);
    }

    public async Task AcceptRequestAsync(string code)
    {
        await AddFriendAsync(code);
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = "req_accept",
            from = MyCode,
            name = DisplayName,
            spk = _mySignPub,
            apk = _myAgreePub,
            s = SignData($"req_accept|{MyCode}|{DisplayName}|{_mySignPub}|{_myAgreePub}")
        });
        await PublishAsync(ReqTopic(code), envelope);
        await SendMyKeysAsync(code);
        await PublishPresenceAsync();
    }

    public void RemoveFriend(string code)
    {
        var topic = PairTopic(MyCode, code);
        if (_friendsByTopic.Remove(topic))
            _ = _client.UnsubscribeAsync(topic);
        _friendSign.Remove(code);
        _friendAgree.Remove(code);
    }

    public async Task InviteToServerAsync(string code, string server)
    {
        await PublishSealedAsync(PairTopic(MyCode, code), code, "invite",
            JsonSerializer.Serialize(new { server }));
    }

    // ─── Скины и плащи ───

    public async Task PublishSkinAsync(string code, string mcName, string? skinBase64, string? capeBase64)
    {
        await PublishSealedAsync(PairTopic(MyCode, code), code, "sk",
            JsonSerializer.Serialize(new
            {
                name = DisplayName,
                mcname = mcName,
                skin = skinBase64 ?? "",
                cape = capeBase64 ?? ""
            }));
    }

    public async Task RequestSkinAsync(string code)
    {
        await PublishSealedAsync(PairTopic(MyCode, code), code, "sk_req", "{}");
    }

    public async Task PublishPresenceAsync(string? server = null, string? status = null)
    {
        if (!_client.IsConnected) return;
        var inner = JsonSerializer.Serialize(new
        {
            name = DisplayName,
            server = server ?? "",
            status = status ?? "",
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        foreach (var kv in _friendsByTopic.ToList())
        {
            try { await PublishSealedAsync(kv.Key, kv.Value, "p", inner); } catch { }
        }
    }

    public async Task SendMessageAsync(string code, string text)
    {
        if (!_client.IsConnected) return;
        await PublishSealedAsync(PairTopic(MyCode, code), code, "c",
            JsonSerializer.Serialize(new
            {
                name = DisplayName,
                text,
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }));
    }

    public async Task SendTypingAsync(string code)
    {
        if (!_client.IsConnected) return;
        await PublishSealedAsync(PairTopic(MyCode, code), code, "tp", "{}");
    }

    // ─── Группы ───

    public async Task JoinGroupAsync(string code)
    {
        _groupCodes.Add(code);
        await SubscribeAsync(GroupTopic(code));
        var key = CryptoHelper.GroupKey(code);
        var (nonce, cipher) = CryptoHelper.Encrypt(key, Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { name = DisplayName })));
        var k = Convert.ToBase64String(nonce);
        var d = Convert.ToBase64String(cipher);
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = "gp",
            from = MyCode,
            spk = _mySignPub,
            k,
            d,
            s = SignData($"gp|{MyCode}|{_mySignPub}|{k}|{d}")
        });
        await PublishAsync(GroupTopic(code), envelope);
    }

    public void LeaveGroup(string code)
    {
        if (_groupCodes.Remove(code))
            _ = _client.UnsubscribeAsync(GroupTopic(code));
    }

    public async Task SendGroupMessageAsync(string code, string text)
    {
        if (!_client.IsConnected) return;
        var key = CryptoHelper.GroupKey(code);
        var (nonce, cipher) = CryptoHelper.Encrypt(key, Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                name = DisplayName,
                text,
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            })));
        var k = Convert.ToBase64String(nonce);
        var d = Convert.ToBase64String(cipher);
        var envelope = JsonSerializer.Serialize(new
        {
            v = 1,
            t = "gc",
            from = MyCode,
            spk = _mySignPub,
            k,
            d,
            s = SignData($"gc|{MyCode}|{_mySignPub}|{k}|{d}")
        });
        try { await PublishAsync(GroupTopic(code), envelope); } catch { }
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
            var type = root.TryGetProperty("t", out var tv) ? tv.GetString() ?? "" : "";
            var from = root.TryGetProperty("from", out var fv) ? fv.GetString() ?? "" : "";
            if (from == MyCode) return;

            // Личный канал пары
            if (_friendsByTopic.TryGetValue(topic, out var code))
            {
                if (type == "pk_req")
                {
                    var spk = root.TryGetProperty("spk", out var s1) ? s1.GetString() ?? "" : "";
                    var apk = root.TryGetProperty("apk", out var a1) ? a1.GetString() ?? "" : "";
                    StoreFriendKey(code, spk, apk);
                    await SendMyKeysAsync(code);
                    return;
                }
                if (type == "pk")
                {
                    var spk = root.TryGetProperty("spk", out var s1) ? s1.GetString() ?? "" : "";
                    var apk = root.TryGetProperty("apk", out var a1) ? a1.GetString() ?? "" : "";
                    StoreFriendKey(code, spk, apk);
                    return;
                }
                HandleSealed(code, type, from, root);
                return;
            }

            // Заявки
            if (topic == ReqTopic(MyCode))
            {
                if (type == "req" || type == "req_accept")
                {
                    var name = root.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                    var spk = root.TryGetProperty("spk", out var s1) ? s1.GetString() ?? "" : "";
                    var apk = root.TryGetProperty("apk", out var a1) ? a1.GetString() ?? "" : "";
                    var sig = root.TryGetProperty("s", out var s2) ? s2.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(spk) || string.IsNullOrEmpty(apk) || string.IsNullOrEmpty(sig)) return;
                    if (!CryptoHelper.Verify(spk, $"{type}|{from}|{name}|{spk}|{apk}", sig)) return;
                    if (type == "req") RequestReceived?.Invoke(from, name, spk, apk);
                    else RequestAccepted?.Invoke(from, name, spk, apk);
                }
                return;
            }

            // Группы
            var groupTopicPrefix = "dedlauncher/v1/grp/";
            if (topic.StartsWith(groupTopicPrefix))
            {
                var groupCode = topic.Substring(groupTopicPrefix.Length);
                HandleSealedGroup(groupCode, type, from, root);
            }
        }
        catch { }
    }

    private void HandleSealed(string code, string type, string from, JsonElement root)
    {
        var spk = root.TryGetProperty("spk", out var s1) ? s1.GetString() ?? "" : "";
        var k = root.TryGetProperty("k", out var k1) ? k1.GetString() ?? "" : "";
        var d = root.TryGetProperty("d", out var d1) ? d1.GetString() ?? "" : "";
        var sig = root.TryGetProperty("s", out var s2) ? s2.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(spk) || string.IsNullOrEmpty(k) || string.IsNullOrEmpty(d) || string.IsNullOrEmpty(sig)) return;

        if (!CryptoHelper.Verify(spk, $"{type}|{from}|{spk}|{k}|{d}", sig)) return;
        if (_friendSign.TryGetValue(code, out var pinned) && pinned != spk) return;
        if (!_friendAgree.TryGetValue(code, out var agreePub)) return;

        var key = CryptoHelper.Derive(_myAgree, agreePub);
        string inner;
        try
        {
            inner = Encoding.UTF8.GetString(CryptoHelper.Decrypt(key, Convert.FromBase64String(k), Convert.FromBase64String(d)));
        }
        catch { return; }

        using var idoc = JsonDocument.Parse(inner);
        var ir = idoc.RootElement;
        var name = ir.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";

        switch (type)
        {
            case "p":
                var server = ir.TryGetProperty("server", out var sv) ? sv.GetString() : "";
                var status = ir.TryGetProperty("status", out var st) ? st.GetString() : "";
                PresenceReceived?.Invoke(code, name, string.IsNullOrEmpty(server) ? null : server,
                    string.IsNullOrEmpty(status) ? null : status, true);
                break;
            case "c":
                var text = ir.TryGetProperty("text", out var tx) ? tx.GetString() : "";
                if (!string.IsNullOrEmpty(text))
                    MessageReceived?.Invoke(code, name, text);
                break;
            case "tp":
                TypingReceived?.Invoke(code);
                break;
            case "invite":
                var svr = ir.TryGetProperty("server", out var si) ? si.GetString() : "";
                InviteReceived?.Invoke(code, svr);
                break;
            case "sk":
                var mcname = ir.TryGetProperty("mcname", out var mn) ? mn.GetString() : name;
                var skin = ir.TryGetProperty("skin", out var sk) ? sk.GetString() : "";
                var cape = ir.TryGetProperty("cape", out var cp) ? cp.GetString() : "";
                SkinReceived?.Invoke(code, mcname,
                    string.IsNullOrEmpty(skin) ? null : skin,
                    string.IsNullOrEmpty(cape) ? null : cape);
                break;
            case "sk_req":
                SkinRequested?.Invoke(code);
                break;
        }
    }

    private void HandleSealedGroup(string groupCode, string type, string from, JsonElement root)
    {
        var spk = root.TryGetProperty("spk", out var s1) ? s1.GetString() ?? "" : "";
        var k = root.TryGetProperty("k", out var k1) ? k1.GetString() ?? "" : "";
        var d = root.TryGetProperty("d", out var d1) ? d1.GetString() ?? "" : "";
        var sig = root.TryGetProperty("s", out var s2) ? s2.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(spk) || string.IsNullOrEmpty(k) || string.IsNullOrEmpty(d) || string.IsNullOrEmpty(sig)) return;
        if (!CryptoHelper.Verify(spk, $"{type}|{from}|{spk}|{k}|{d}", sig)) return;

        var key = CryptoHelper.GroupKey(groupCode);
        string inner;
        try
        {
            inner = Encoding.UTF8.GetString(CryptoHelper.Decrypt(key, Convert.FromBase64String(k), Convert.FromBase64String(d)));
        }
        catch { return; }

        using var idoc = JsonDocument.Parse(inner);
        var ir = idoc.RootElement;
        var name = ir.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";

        if (type == "gp")
        {
            GroupPresence?.Invoke(groupCode, name);
        }
        else if (type == "gc")
        {
            var text = ir.TryGetProperty("text", out var tx) ? tx.GetString() : "";
            if (!string.IsNullOrEmpty(text)) GroupMessage?.Invoke(groupCode, name, text);
        }
    }

    public void Dispose()
    {
        try { _client.Dispose(); } catch { }
        try { _mySign.Dispose(); } catch { }
        try { _myAgree.Dispose(); } catch { }
    }
}
