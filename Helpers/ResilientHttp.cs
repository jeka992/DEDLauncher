using System.Net;

namespace DedLauncher.Helpers;

/// <summary>
/// HTTP-клиент с автоматическими повторными попытками на транзиентные ошибки
/// (по образцу XMCL: retry на сброс соединения/таймауты/429/5xx, exponential backoff).
/// </summary>
public static class ResilientHttp
{
    private static readonly HttpClient _client = CreateClient();

    public static HttpClient Client => _client;

    public static HttpClient CreateClient(int maxRetries = 3)
    {
        var inner = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(25)
        };
        var handler = new ResilientHandler(maxRetries) { InnerHandler = inner };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(100)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DedLauncher/2.0");
        return client;
    }

    /// <summary>
    /// DelegatingHandler: повторяет запрос с экспоненциальной паузой
    /// (1s → 2s → 4s) на таймауты, сброс соединения и ответы 429/5xx.
    /// </summary>
    private sealed class ResilientHandler : DelegatingHandler
    {
        private readonly int _maxRetries;

        public ResilientHandler(int maxRetries) => _maxRetries = maxRetries;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    var response = await base.SendAsync(request.Clone(), cancellationToken);
                    bool transient = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                    if (!transient || attempt > _maxRetries) return response;

                    response.Dispose();
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    if (attempt > _maxRetries) throw;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
                }
                catch (HttpRequestException)
                {
                    if (attempt > _maxRetries) throw;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
                }
            }
        }
    }
}

internal static class HttpRequestMessageExtensions
{
    /// <summary>Клонирует HttpRequestMessage для повторной отправки.</summary>
    public static HttpRequestMessage Clone(this HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var prop in req.Options)
            clone.Options.TryAdd(prop.Key, prop.Value);
        return clone;
    }
}