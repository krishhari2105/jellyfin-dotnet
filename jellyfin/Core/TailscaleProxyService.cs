using System;
using System.IO;
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

        public static string LocalProxyAddress => "127.0.0.1";
        public static int LocalProxyPort => 8123;
        public static string LocalProxyUrl => $"http://{LocalProxyAddress}:{LocalProxyPort}";

        public TailscaleProxyService(HttpClient httpClient)
        {
            // Create a dedicated inner client with TailscaleWebProxy for forwarding,
            // so requests to Tailscale IPs go through tailscaled.
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

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{LocalProxyAddress}:{LocalProxyPort}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
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
                // Ignore cleanup errors
            }

            try
            {
                if (_listenerTask != null)
                    _listenerTask.Wait(2000);
            }
            catch
            {
                // Ignore wait errors
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

                if (proxyResponse.Content.Headers.ContentLength.HasValue)
                    response.ContentLength64 = proxyResponse.Content.Headers.ContentLength.Value;

                await proxyResponse.Content.CopyToAsync(response.OutputStream, cancellationToken);
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
            _disposed = true;
        }
    }
}