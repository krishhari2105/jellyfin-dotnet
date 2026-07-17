using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        private const long MemoryCacheMaxBytes = 32L * 1024 * 1024;
        private static readonly object _imageCacheLock = new();
        private static long _imageCacheBytes;

        // Persistent on-disk image cache. Survives across sessions so re-browsing a
        // library is instant and never re-hits the (weak) server for the same image.
        private const long DiskCacheMaxBytes = 120L * 1024 * 1024; // ~120 MB budget
        private static readonly TimeSpan _diskCacheTtl = TimeSpan.FromDays(30);
        private static readonly object _diskCacheLock = new();
        private static string _diskCacheDir; // null = not resolved yet, "" = disabled
        private static int _diskWritesSinceTrim;

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
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listener, _cts.Token));
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
            var listener = _listener;
            if (listener == null)
                return;

            try
            {
                _cts?.Cancel();
                listener.Stop();
                listener.Close();
                _listener = null;
            }
            catch
            {
            }

            _cts?.Dispose();
            _cts = null;
            _listenerTask = null;
        }

        private async Task ListenLoopAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
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

                // Fast path: serve a previously cached image (in-memory, then on-disk)
                // without any upstream request. Keyed on the URL minus its volatile auth
                // token so the same image stays cached across sessions/logins. Non-image
                // requests (e.g. video) are never written to the cache, so this is always
                // a miss for them and falls through to normal proxying.
                string cacheKey = NormalizeCacheKey(targetUrl);
                if (await TryServeCachedImageAsync(cacheKey, response, cancellationToken))
                {
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
                        // Cache miss (the fast path above already handled hits): download the
                        // image fully, then persist it to memory + disk before responding.
                        // Buffering also survives Tizen ImageView's early connection close.
                        using var memoryStream = new MemoryStream();
                        await proxyResponse.Content.CopyToAsync(memoryStream, cancellationToken);
                        var imageData = memoryStream.ToArray();

                        if (proxyResponse.IsSuccessStatusCode && imageData.Length < 5 * 1024 * 1024) // 5MB max per image
                        {
                            StoreInMemory(cacheKey, imageData, contentType);
                            await WriteDiskCacheAsync(cacheKey, imageData, contentType);
                        }

                        response.ContentLength64 = imageData.Length;
                        await response.OutputStream.WriteAsync(imageData, 0, imageData.Length, cancellationToken);
                        await response.OutputStream.FlushAsync(cancellationToken);
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
            lock (_imageCacheLock)
            {
                _imageCache.Clear();
                _imageCacheBytes = 0;
            }

            try
            {
                string dir = EnsureDiskCacheDir();
                if (!string.IsNullOrEmpty(dir))
                {
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Proxy: disk cache clear failed: {ex.Message}");
            }
        }

        // ---- Image cache helpers (memory + disk) ----

        // Serve a cached image without any upstream request. Returns true if served.
        private async Task<bool> TryServeCachedImageAsync(
            string cacheKey, HttpListenerResponse response, CancellationToken cancellationToken)
        {
            byte[] data = null;
            string contentType = null;

            if (_imageCache.TryGetValue(cacheKey, out var cached) &&
                DateTime.UtcNow - cached.CachedAt < _cacheExpiration)
            {
                data = cached.Data;
                contentType = cached.ContentType;
            }
            else
            {
                if (cached != null)
                    RemoveFromMemory(cacheKey, cached);

                if (TryReadDiskCache(cacheKey, out var diskData, out var diskContentType))
                {
                    data = diskData;
                    contentType = diskContentType;
                    // Promote to memory so subsequent hits this session skip the disk read.
                    StoreInMemory(cacheKey, diskData, diskContentType);
                }
            }

            if (data == null)
                return false;

            try
            {
                TailscaleDebugLog.Add($"Proxy: Serving image from cache ({data.Length} bytes)");
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = string.IsNullOrEmpty(contentType) ? "image/jpeg" : contentType;
                response.ContentLength64 = data.Length;
                await response.OutputStream.WriteAsync(data, 0, data.Length, cancellationToken);
                await response.OutputStream.FlushAsync(cancellationToken);
            }
            catch (HttpListenerException)
            {
                TailscaleDebugLog.Add("Proxy: Client disconnected during cache serve");
            }
            catch (OperationCanceledException)
            {
            }

            return true;
        }

        private static void StoreInMemory(string cacheKey, byte[] data, string contentType)
        {
            if (data == null || data.Length == 0 || data.Length >= 5 * 1024 * 1024)
                return;

            lock (_imageCacheLock)
            {
                if (_imageCache.TryRemove(cacheKey, out var existing))
                    _imageCacheBytes -= existing.Data?.LongLength ?? 0;

                // Bound the in-memory cache by both count and bytes. The byte budget
                // is the meaningful limit on a long-running TV process.
                while (_imageCache.Count >= _maxCacheSize || _imageCacheBytes + data.LongLength > MemoryCacheMaxBytes)
                {
                    var oldest = _imageCache.OrderBy(kvp => kvp.Value.CachedAt).FirstOrDefault();
                    if (oldest.Key == null || !_imageCache.TryRemove(oldest.Key, out var removed))
                        break;

                    _imageCacheBytes -= removed.Data?.LongLength ?? 0;
                }

                if (_imageCacheBytes + data.LongLength > MemoryCacheMaxBytes)
                    return;

                _imageCache[cacheKey] = new CachedImage
                {
                    Data = data,
                    ContentType = string.IsNullOrEmpty(contentType) ? "image/jpeg" : contentType,
                    CachedAt = DateTime.UtcNow
                };
                _imageCacheBytes += data.LongLength;
            }
        }

        private static void RemoveFromMemory(string cacheKey, CachedImage expected)
        {
            lock (_imageCacheLock)
            {
                if (_imageCache.TryGetValue(cacheKey, out var current) && ReferenceEquals(current, expected) &&
                    _imageCache.TryRemove(cacheKey, out var removed))
                {
                    _imageCacheBytes -= removed.Data?.LongLength ?? 0;
                }
            }
        }

        // Removes the volatile auth token from the URL so the same image caches across
        // sessions/logins (the bytes are identical regardless of api_key).
        private static string NormalizeCacheKey(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            string result = Regex.Replace(
                url,
                @"([?&])(api_key|X-Emby-Token|ApiKey)=[^&]*",
                "$1",
                RegexOptions.IgnoreCase);

            // Collapse separators left behind by the removal.
            result = result.Replace("?&", "?").Replace("&&", "&").TrimEnd('?', '&');
            return result;
        }

        private static string EnsureDiskCacheDir()
        {
            if (_diskCacheDir != null)
                return _diskCacheDir;

            lock (_diskCacheLock)
            {
                if (_diskCacheDir != null)
                    return _diskCacheDir;

                try
                {
                    string dir = Path.Combine(Application.Current.DirectoryInfo.Data, "image-cache");
                    Directory.CreateDirectory(dir);
                    _diskCacheDir = dir;
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"Proxy: disk cache unavailable, disabling: {ex.Message}");
                    _diskCacheDir = string.Empty; // sentinel: disabled
                }
            }

            return _diskCacheDir;
        }

        private static string HashKey(string cacheKey)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(cacheKey ?? string.Empty));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string GetCacheFilePath(string cacheKey, string suffix)
        {
            string dir = EnsureDiskCacheDir();
            if (string.IsNullOrEmpty(dir))
                return null;
            return Path.Combine(dir, HashKey(cacheKey) + suffix);
        }

        private static bool TryReadDiskCache(string cacheKey, out byte[] data, out string contentType)
        {
            data = null;
            contentType = null;

            string dataPath = GetCacheFilePath(cacheKey, ".img");
            if (dataPath == null)
                return false;

            try
            {
                if (!File.Exists(dataPath))
                    return false;

                var info = new FileInfo(dataPath);
                if (DateTime.UtcNow - info.LastWriteTimeUtc > _diskCacheTtl)
                {
                    TryDeleteCacheEntry(cacheKey);
                    return false;
                }

                data = File.ReadAllBytes(dataPath);
                if (data.Length == 0)
                    return false;

                string ctPath = GetCacheFilePath(cacheKey, ".ct");
                contentType = (ctPath != null && File.Exists(ctPath))
                    ? File.ReadAllText(ctPath)
                    : "image/jpeg";

                // Touch for LRU trimming.
                try { File.SetLastWriteTimeUtc(dataPath, DateTime.UtcNow); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Proxy: disk cache read failed: {ex.Message}");
                data = null;
                contentType = null;
                return false;
            }
        }

        private static async Task WriteDiskCacheAsync(string cacheKey, byte[] data, string contentType)
        {
            if (data == null || data.Length == 0)
                return;

            string dataPath = GetCacheFilePath(cacheKey, ".img");
            if (dataPath == null)
                return;

            try
            {
                string tmpPath = dataPath + ".tmp";
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                {
                    await fs.WriteAsync(data, 0, data.Length);
                }

                try { if (File.Exists(dataPath)) File.Delete(dataPath); } catch { }
                File.Move(tmpPath, dataPath);

                string ctPath = GetCacheFilePath(cacheKey, ".ct");
                if (ctPath != null)
                    File.WriteAllText(ctPath, string.IsNullOrEmpty(contentType) ? "image/jpeg" : contentType);

                MaybeTrimDiskCache();
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Proxy: disk cache write failed: {ex.Message}");
            }
        }

        private static void TryDeleteCacheEntry(string cacheKey)
        {
            try
            {
                string dataPath = GetCacheFilePath(cacheKey, ".img");
                string ctPath = GetCacheFilePath(cacheKey, ".ct");
                if (dataPath != null && File.Exists(dataPath)) File.Delete(dataPath);
                if (ctPath != null && File.Exists(ctPath)) File.Delete(ctPath);
            }
            catch { }
        }

        private static void MaybeTrimDiskCache()
        {
            // Only scan the cache dir occasionally to keep writes cheap.
            if (Interlocked.Increment(ref _diskWritesSinceTrim) < 40)
                return;

            Interlocked.Exchange(ref _diskWritesSinceTrim, 0);
            _ = Task.Run(TrimDiskCache);
        }

        private static void TrimDiskCache()
        {
            string dir = EnsureDiskCacheDir();
            if (string.IsNullOrEmpty(dir))
                return;

            lock (_diskCacheLock)
            {
                try
                {
                    var files = new System.IO.DirectoryInfo(dir).GetFiles("*.img");
                    long total = files.Sum(f => f.Length);
                    if (total <= DiskCacheMaxBytes)
                        return;

                    foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc))
                    {
                        if (total <= DiskCacheMaxBytes)
                            break;

                        long len = file.Length;
                        try
                        {
                            file.Delete();
                            string ctPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(file.Name) + ".ct");
                            if (File.Exists(ctPath)) File.Delete(ctPath);
                            total -= len;
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"Proxy: disk cache trim failed: {ex.Message}");
                }
            }
        }
    }
}
