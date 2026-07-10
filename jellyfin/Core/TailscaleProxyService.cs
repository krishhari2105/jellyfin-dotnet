using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

namespace JellyfinTizen.Core
{
    public class TailscaleProxyService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly HttpClient _forwardClient;
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listenerTask;
        private bool _disposed;

        // In-memory cache for images to speed up repeated loads
        private static readonly ConcurrentDictionary<string, CachedImage> _imageCache = new();
        private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);
        private static readonly int _maxCacheSize = 100; // Max images to cache

        private sealed class CachedImage
        {
            public byte[] Data;
            public string ContentType;
            public DateTime CachedAt;
        }

        public static string LocalProxyAddress => "127.0.0.1";
        private static int _localProxyPort = 8123;
        public static int LocalProxyPort
        {
            get => _localProxyPort;
            set
            {
                _localProxyPort = value;
                LocalProxyUrl = $"http://{LocalProxyAddress}:{value}";
            }
        }
        public static string LocalProxyUrl { get; private set; } = $"http://{LocalProxyAddress}:8123";

        public TailscaleProxyService(HttpClient httpClient)
        {
            var baseHandler = new HttpClientHandler
            {
                Proxy = new TailscaleWebProxy(),
                UseProxy = true
            };
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _forwardClient = new HttpClient(baseHandler);
        }

        public void Start()
        {
            if (_listener != null)
                return;

            int retries = 5;
            while (retries > 0)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://{LocalProxyAddress}:{LocalProxyPort}/");
                    _listener.Start();

                    _cts = new CancellationTokenSource();
                    _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
                    TailscaleDebugLog.Add("TailscaleProxyService listener started successfully.");
                    break;
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"Failed to start TailscaleProxyService (retries left: {retries - 1}): {ex.Message}");
                    _listener = null;
                    retries--;
                    if (retries > 0)
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public void Stop()
        {
            if (_listener == null)
                return;

            try
            {
                _cts?.Cancel();
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }
            catch
            {
            }

            try
            {
                if (_listenerTask != null)
                    _listenerTask.Wait(2000);
            }
            catch
            {
            }

            _cts?.Dispose();
            _cts = null;
            _listenerTask = null;
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context, cancellationToken);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Tizen.Log.Error("TailscaleProxy", $"Listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (!request.Url.AbsolutePath.Equals("/proxy", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                string targetUrl = request.QueryString["url"];
                if (string.IsNullOrWhiteSpace(targetUrl))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var errorBytes = Encoding.UTF8.GetBytes("Missing url parameter");
                    await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
                    return;
                }

                if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var errorBytes = Encoding.UTF8.GetBytes("Invalid url parameter");
                    await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
                    return;
                }

                TailscaleDebugLog.Add($"Proxying request for: {targetUrl}");
                using var proxyRequest = new HttpRequestMessage(HttpMethod.Get, uri);
                foreach (string headerName in request.Headers)
                {
                    if (string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase))
                        continue;

                    proxyRequest.Headers.TryAddWithoutValidation(headerName, request.Headers[headerName]);
                }

                using var proxyResponse = await _forwardClient.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                TailscaleDebugLog.Add($"Proxy response: {(int)proxyResponse.StatusCode} {proxyResponse.StatusCode}");
                response.StatusCode = (int)proxyResponse.StatusCode;
                response.ContentType = proxyResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                // Forward Content-Range for 206 Partial Content
                if (proxyResponse.StatusCode == (HttpStatusCode)206 &&
                    proxyResponse.Content.Headers.ContentRange != null)
                {
                    response.AddHeader("Content-Range", proxyResponse.Content.Headers.ContentRange.ToString());
                }

                try
                {
                    // Check if this is an image or video based on content type
                    string contentType = proxyResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
                    bool isImageContent = contentType.Contains("image/");

                    if (isImageContent)
                    {
                        // Check cache first for faster subsequent loads
                        string cacheKey = targetUrl;
                        if (_imageCache.TryGetValue(cacheKey, out var cached) &&
                            DateTime.UtcNow - cached.CachedAt < _cacheExpiration)
                        {
                            TailscaleDebugLog.Add($"Proxy: Serving image from cache: {targetUrl}");
                            response.ContentLength64 = cached.Data.Length;
                            response.ContentType = cached.ContentType;
                            await response.OutputStream.WriteAsync(cached.Data, 0, cached.Data.Length, cancellationToken);
                            await response.OutputStream.FlushAsync(cancellationToken);
                        }
                        else
                        {
                            // Buffer images fully to survive Tizen ImageView's early connection close
                            using var memoryStream = new MemoryStream();
                            await proxyResponse.Content.CopyToAsync(memoryStream, cancellationToken);
                            var imageData = memoryStream.ToArray();

                            // Cache the image for future requests (with size limit) - ONLY if response is successful
                            if (proxyResponse.IsSuccessStatusCode && imageData.Length < 5 * 1024 * 1024 && _imageCache.Count < _maxCacheSize) // 5MB max per image
                            {
                                _imageCache[cacheKey] = new CachedImage
                                {
                                    Data = imageData,
                                    ContentType = contentType,
                                    CachedAt = DateTime.UtcNow
                                };

                                // Trim cache if too large
                                if (_imageCache.Count > _maxCacheSize)
                                {
                                    var oldestEntry = _imageCache.OrderBy(kvp => kvp.Value.CachedAt).FirstOrDefault();
                                    if (!oldestEntry.Equals(default))
                                    {
                                        _imageCache.TryRemove(oldestEntry.Key, out _);
                                    }
                                }
                            }

                            response.ContentLength64 = imageData.Length;
                            await response.OutputStream.WriteAsync(imageData, 0, imageData.Length, cancellationToken);
                            await response.OutputStream.FlushAsync(cancellationToken);
                        }
                    }
                    else
                    {
                        // Stream video/audio directly - player keeps connection open
                        // Use chunked encoding so we don't need to know content length in advance
                        response.SendChunked = true;
                        using var sourceStream = await proxyResponse.Content.ReadAsStreamAsync();
                        var buffer = new byte[65536];
                        int bytesRead;
                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            try
                            {
                                await response.OutputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            }
                            catch (HttpListenerException)
                            {
                                // Client disconnected during seek/stop - clean abort
                                break;
                            }
                        }
                    }
                }
                catch (HttpListenerException)
                {
                    TailscaleDebugLog.Add("Proxy: Client disconnected");
                }
                catch (OperationCanceledException)
                {
                    TailscaleDebugLog.Add("Proxy: Request was cancelled");
                }
            }
            catch (OperationCanceledException)
            {
                TailscaleDebugLog.Add("Proxy request cancelled or timed out.");
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            }
            catch (HttpRequestException ex)
            {
                TailscaleDebugLog.Add($"Proxy HTTP request failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    TailscaleDebugLog.Add($"Proxy HTTP inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                Tizen.Log.Error("TailscaleProxy", $"Proxy request failed: {ex.Message}");
                response.StatusCode = (int)HttpStatusCode.BadGateway;
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Proxy unexpected error: {ex.GetType().Name}: {ex.Message}");
                Tizen.Log.Error("TailscaleProxy", $"Unexpected proxy error: {ex.Message}");
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _forwardClient?.Dispose();
            _disposed = true;
        }

        public static void ClearCache()
        {
            _imageCache.Clear();
        }
    }
}