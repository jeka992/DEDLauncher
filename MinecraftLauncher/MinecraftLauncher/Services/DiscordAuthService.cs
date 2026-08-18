using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DedLauncher.Services;







public class DiscordAuthService
{
    
    
    public const string ClientId = "1538107296276545629";

    private readonly HttpClient _http;

    public DiscordAuthService(HttpClient http)
    {
        _http = http;
    }

    public record DiscordUser(string Id, string Username, string AvatarUrl);

    public async Task<DiscordUser?> LoginAsync(CancellationToken ct = default)
    {
        const int port = 18000;
        var redirectUri = $"http://localhost:{port}/callback";

        
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        var authUrl = $"https://discord.com/oauth2/authorize" +
                      $"?client_id={ClientId}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      $"&response_type=code" +
                      $"&scope=identify" +
                      $"&prompt=consent" +
                      $"&code_challenge={codeChallenge}" +
                      $"&code_challenge_method=S256";

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(authUrl) { UseShellExecute = true }); }
        catch { listener.Stop(); return null; }

        
        string? code = null;
        var timeout = Task.Delay(TimeSpan.FromMinutes(2), ct);
        var gotContext = listener.GetContextAsync();

        var finished = await Task.WhenAny(gotContext, timeout);
        if (finished == timeout)
        {
            listener.Stop();
            return null;
        }

        var ctx = gotContext.Result;
        var query = ctx.Request.QueryString;
        code = query["code"];
        var error = query["error"];

        var html = error != null
            ? "cyr1"
            : "cyr2";
        var bytes = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.OutputStream.Close();
        listener.Stop();

        if (string.IsNullOrEmpty(code)) return null;

        
        var tokenResponse = await _http.PostAsync("https://discord.com/api/oauth2/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            }), ct);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString();
        if (string.IsNullOrEmpty(accessToken)) return null;

        
        var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userResponse = await _http.SendAsync(req, ct);
        userResponse.EnsureSuccessStatusCode();
        var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(ct));
        var root = userJson.RootElement;

        var id = root.GetProperty("id").GetString() ?? "";
        var username = root.GetProperty("username").GetString() ?? "Discord";
        var avatarHash = root.TryGetProperty("avatar", out var av) ? av.GetString() : null;

        var avatarUrl = avatarHash != null
            ? $"https://cdn.discordapp.com/avatars/{id}/{avatarHash}.png?size=128"
            : "";

        return new DiscordUser(id, username, avatarUrl);
    }

    
    public static string UuidFromDiscordId(string discordId)
    {
        var data = Encoding.UTF8.GetBytes("Discord:" + discordId);
        var hash = MD5.HashData(data);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash).ToString("N");
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
