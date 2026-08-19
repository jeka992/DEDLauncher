using System.Net.Http;
using System.Text.RegularExpressions;

namespace DedLauncher.Services;

/// <summary>
/// Проверка обновлений через Telegram-канал: лаунчер парсит публичную
/// веб-версию канала (t.me/s/{канал} с зеркалами) и ищет последний пост
/// #update с версией и ссылкой на скачивание.
///
/// Новые версии раздаются вручную (Telegram/Discord): лаунчер только
/// показывает, что есть обновление, и открывает ссылку в браузере.
/// Установка новой копии сама предлагает обновить существующую.
///
/// Формат поста в канале:
///   #update 2.0.1
///   https://.../DEDLauncher_2.0.1.zip
/// </summary>
public static class UpdateService
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string Url { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        return client;
    }

    private static readonly string[] TgEndpoints =
    {
        "https://t.me/s/{0}",
        "https://r.jina.ai/https://t.me/s/{0}",
        "https://tg.i-c-a.su/s/{0}"
    };

    /// <summary>Ищет в канале последний пост #update. null — ничего нет или канал недоступен.</summary>
    public static async Task<UpdateInfo?> CheckTgAsync(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;
        channel = channel.Trim().TrimStart('@');

        foreach (var tpl in TgEndpoints)
        {
            try
            {
                var content = await Http.GetStringAsync(string.Format(tpl, channel));
                var info = ParseContent(content);
                if (info != null) return info;
            }
            catch { }
        }
        return null;
    }

    private static UpdateInfo? ParseContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var isHtml = content.Contains("data-post", StringComparison.OrdinalIgnoreCase)
                     || content.Contains("<div", StringComparison.OrdinalIgnoreCase);
        return isHtml ? ParseHtml(content) : ParseText(content);
    }

    private static UpdateInfo? ParseHtml(string html)
    {
        var postStarts = Regex.Matches(html, "data-post=\"([^\"]+)\"");
        for (int i = 0; i < postStarts.Count; i++)
        {
            var start = postStarts[i].Index;
            var end = i + 1 < postStarts.Count ? postStarts[i + 1].Index : html.Length;
            var segment = html[start..end];

            var textMatch = Regex.Match(segment,
                "tgme_widget_message_text[^>]*>(.*?)</div>",
                RegexOptions.Singleline);
            if (!textMatch.Success) continue;

            var text = System.Net.WebUtility.HtmlDecode(
                Regex.Replace(textMatch.Groups[1].Value, "<[^>]+>", " "));

            var verMatch = Regex.Match(text, @"#update\s+v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (!verMatch.Success) continue;

            var links = Regex.Matches(segment, "href=\"(https?://[^\"]+)\"")
                .Cast<Match>()
                .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
                .Where(u => !u.Contains("?q=%23", StringComparison.OrdinalIgnoreCase)   // хештеги
                            && !u.EndsWith("/s/", StringComparison.OrdinalIgnoreCase)) // сам канал
                .ToList();

            // Внешняя ссылка в приоритете, иначе — ссылка на пост t.me
            // (документ, залитый прямо в Telegram, ссылается на пост)
            var url = links.FirstOrDefault(u => !u.Contains("t.me/", StringComparison.OrdinalIgnoreCase)
                                                && !u.Contains("telegram.org", StringComparison.OrdinalIgnoreCase))
                      ?? links.FirstOrDefault();
            if (string.IsNullOrEmpty(url)) continue;

            return new UpdateInfo { Version = verMatch.Groups[1].Value, Url = url, Text = text.Trim() };
        }
        return null;
    }

    private static UpdateInfo? ParseText(string text)
    {
        var verMatch = Regex.Match(text, @"#update\s+v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (!verMatch.Success) return null;

        var links = Regex.Matches(text, @"https?://[^\s)\]]+")
            .Cast<Match>()
            .Select(m => m.Value.TrimEnd('.', ','))
            .ToList();
        var url = links.FirstOrDefault(u => !u.Contains("t.me/", StringComparison.OrdinalIgnoreCase)
                                            && !u.Contains("telegram.org", StringComparison.OrdinalIgnoreCase))
                  ?? links.FirstOrDefault();
        if (string.IsNullOrEmpty(url)) return null;

        return new UpdateInfo { Version = verMatch.Groups[1].Value, Url = url, Text = text.Trim() };
    }

    /// <summary>Сравнение версий: >0 если a новее b.</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = ParseVersion(a);
        var pb = ParseVersion(b);
        for (int i = 0; i < 3; i++)
        {
            if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
        }
        return 0;
    }

    private static int[] ParseVersion(string v)
    {
        var parts = (v ?? "").Split('.');
        var res = new int[3];
        for (int i = 0; i < 3; i++)
            res[i] = i < parts.Length && int.TryParse(parts[i], out var n) ? n : 0;
        return res;
    }
}
