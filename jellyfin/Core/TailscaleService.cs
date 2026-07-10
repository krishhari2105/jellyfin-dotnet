using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Tizen.Applications;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JellyfinTizen.Core
{
    public class TailscaleService : IDisposable
    {
        private Process _tailscaledProc;
        private string _stateDir;
        private string _socket;
        private string _tailscaledExe;
        private bool _disposed;
        private string _lastAuthUrl;
        private HttpClient _localApiClient;

        private static readonly Regex AuthUrlRegex = new(
            @"(?:AuthURL is|AuthURL=|BrowseToURL=)\s*(?<url>https?://\S+)|(?<url>https://login\.tailscale\.com/\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool IsRunning => _tailscaledProc != null && !_tailscaledProc.HasExited;
        public string LastAuthUrl => _lastAuthUrl;
        public event Action<string> AuthUrlReceived;

        /// <summary>Clears the cached auth URL once the tailnet is authenticated.</summary>
        public void ClearAuthUrl() => _lastAuthUrl = null;

        /// <summary>True if the tailscaled Unix socket file exists, meaning the daemon is (or was) reachable.
        /// This stays true even if the Tizen app restarted but tailscaled kept running.</summary>
        public bool IsSocketReachable => !string.IsNullOrEmpty(_socket) && System.IO.File.Exists(_socket);

        public static string ProxyAddress => "127.0.0.1";
        public static int PeerToPeerPort { get; private set; } = 41641;
        public static int HttpProxyPort { get; private set; } = 3128;
        public static int Socks5ProxyPort { get; private set; } = 1080;
        public static string HttpProxyUrl => $"http://{ProxyAddress}:{HttpProxyPort}";
        public static string Socks5ProxyUrl => $"socks5://{ProxyAddress}:{Socks5ProxyPort}";

        public async Task StageAndStart()
        {
            string installDir = Application.Current.DirectoryInfo.Resource;
            string dataDir = Application.Current.DirectoryInfo.Data;
            _stateDir = Path.Combine(dataDir, "tailscale-state");
            Directory.CreateDirectory(_stateDir);
            
            // Clean up any stale socket files from previous runs
            try
            {
                if (Directory.Exists(_stateDir))
                {
                    foreach (var file in Directory.GetFiles(_stateDir, "*.sock"))
                    {
                        try 
                        { 
                            TailscaleDebugLog.Add($"Deleting stale socket file: {file}");
                            File.Delete(file); 
                        } 
                        catch { }
                    }
                }
            }
            catch { }

            // Use a unique socket name for this specific app instance (PID-based)
            _socket = Path.Combine(_stateDir, $"tailscaled_{System.Diagnostics.Process.GetCurrentProcess().Id}.sock");
            _tailscaledExe = Path.Combine(dataDir, "tailscaled");

            // Allocate free ports to avoid conflicts with frozen processes
            try
            {
                var udpPorts = GetFreeUdpPorts(1, 41641);
                PeerToPeerPort = udpPorts[0];
                var tcpPorts = GetFreePorts(2, 3128, 1080);
                HttpProxyPort = tcpPorts[0];
                Socks5ProxyPort = tcpPorts[1];
                TailscaleDebugLog.Add($"Allocated dynamic ports: P2P={PeerToPeerPort}, HTTP Proxy={HttpProxyPort}, Socks5 Proxy={Socks5ProxyPort}");
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Warning: Port allocation failed ({ex.Message}), using default fallback ports");
                PeerToPeerPort = 41641;
                HttpProxyPort = 3128;
                Socks5ProxyPort = 1080;
            }

            try
            {
                TailscaleDebugLog.Add("Checking for orphaned tailscaled processes...");
                var processes = Process.GetProcessesByName("tailscaled");
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.Id != System.Diagnostics.Process.GetCurrentProcess().Id)
                        {
                            TailscaleDebugLog.Add($"Killing orphaned tailscaled process (PID: {p.Id})");
                            p.Kill();
                            p.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        TailscaleDebugLog.Add($"Warning: Failed to kill orphaned process: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Warning: Failed to scan/kill tailscaled processes: {ex.Message}");
            }

            // Detect architecture and pick the right binary
            // Both ARM and x86 binaries can be placed in lib/ folder
            string armBinary = Path.Combine(installDir, "..", "lib", "tailscaled").Replace("\\", "/");
            string x86Binary = Path.Combine(installDir, "..", "lib", "tailscaled-x86").Replace("\\", "/");
            armBinary = Path.GetFullPath(armBinary);
            x86Binary = Path.GetFullPath(x86Binary);
            
            TailscaleDebugLog.Add($"Checking ARM binary at: {armBinary}");
            TailscaleDebugLog.Add($"ARM binary exists: {File.Exists(armBinary)}");
            TailscaleDebugLog.Add($"Checking x86 binary at: {x86Binary}");
            TailscaleDebugLog.Add($"x86 binary exists: {File.Exists(x86Binary)}");
            
            string sourceBinary = null;
            
            // Check if we're on x86 (emulator) by examining the process architecture
            // Since we can't easily detect it, check both binaries and use the one that exists
            // x86 emulator will have tailscaled-x86, real TVs will have tailscaled (ARM)
            
            if (File.Exists(x86Binary))
            {
                sourceBinary = x86Binary;
                TailscaleDebugLog.Add("Using x86 binary (emulator detected)");
            }
            else if (File.Exists(armBinary))
            {
                sourceBinary = armBinary;
                TailscaleDebugLog.Add("Using ARM binary (real device detected)");
            }
            
            if (sourceBinary == null)
            {
                // Neither binary found
                throw new FileNotFoundException(
                    "tailscaled binary not found. Expected at:\n" +
                    $"  ARM: {armBinary}\n" +
                    $"  x86: {x86Binary}\n\n" +
                    "Build for your target:\n" +
                    "  make tailscaled       # For real TV (ARM)\n" +
                    "  make tailscaled-x86   # For emulator (x86)",
                    armBinary);
            }
            
            // Verify architecture
            try
            {
                using var fs = File.OpenRead(sourceBinary);
                using var br = new BinaryReader(fs);
                br.BaseStream.Seek(18, SeekOrigin.Begin);
                ushort e_machine = br.ReadUInt16();
                // EM_386 = 3, EM_X86_64 = 62, EM_ARM = 40, EM_AARCH64 = 183
                string arch = e_machine switch
                {
                    3 => "x86",
                    62 => "x86_64",
                    40 => "ARM32",
                    183 => "ARM64",
                    _ => $"0x{e_machine:X4}"
                };
                TailscaleDebugLog.Add($"Binary architecture: {arch} (e_machine=0x{e_machine:X4})");
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"WARNING: Could not read ELF header: {ex.Message}");
            }

            File.Copy(sourceBinary, _tailscaledExe, overwrite: true);
            chmod(_tailscaledExe, 0x1ED); // 0755
            TailscaleDebugLog.Add($"Binary copied to: {_tailscaledExe}");

            // Start tailscaled with userspace networking and local proxies
            var psi = new ProcessStartInfo
            {
                FileName = _tailscaledExe,
                WorkingDirectory = dataDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            psi.ArgumentList.Add("--tun=userspace-networking");
            psi.ArgumentList.Add("--statedir=" + _stateDir);
            psi.ArgumentList.Add("--socket=" + _socket);
            psi.ArgumentList.Add("--state=" + Path.Combine(_stateDir, "tailscaled.state"));
            psi.ArgumentList.Add("--port=" + PeerToPeerPort);
            psi.ArgumentList.Add("--verbose=1");
            psi.ArgumentList.Add("--socks5-server=" + $"{ProxyAddress}:{Socks5ProxyPort}");
            psi.ArgumentList.Add("--outbound-http-proxy-listen=" + $"{ProxyAddress}:{HttpProxyPort}");

            TailscaleDebugLog.Add($"Starting tailscaled: {_tailscaledExe}");
            TailscaleDebugLog.Add($"Arguments: --tun=userspace-networking --port={PeerToPeerPort} --statedir={_stateDir} --socket={_socket}");
            
            _tailscaledProc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            
            try
            {
                _tailscaledProc.Start();
                TailscaleDebugLog.Add("tailscaled process started successfully");
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"ERROR: Failed to start tailscaled: {ex.GetType().Name}: {ex.Message}");
                _tailscaledProc = null;
                throw;
            }

            _tailscaledProc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    HandleTailscaledOutput("[tailscaled stdout] " + e.Data);
            };
            _tailscaledProc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    HandleTailscaledOutput("[tailscaled stderr] " + e.Data);
            };
            _tailscaledProc.Exited += (s, e) =>
            {
                TailscaleDebugLog.Add($"ERROR: tailscaled exited! HasExited={_tailscaledProc.HasExited}, ExitCode={_tailscaledProc.ExitCode}");
            };

            _tailscaledProc.BeginOutputReadLine();
            _tailscaledProc.BeginErrorReadLine();

            TailscaleDebugLog.Add("Waiting 2 seconds for initialization...");
            
            // Give it a moment to initialize
            try
            {
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"ERROR: Delay failed: {ex.Message}");
            }

            TailscaleDebugLog.Add($"After delay: IsRunning={IsRunning}, HasExited={_tailscaledProc.HasExited}");
            
            if (_tailscaledProc.HasExited)
            {
                TailscaleDebugLog.Add($"ERROR: tailscaled exited immediately with code: {_tailscaledProc.ExitCode}");
            }
        }

        private void HandleTailscaledOutput(string line)
        {
            CaptureAuthUrl(line);
            TailscaleDebugLog.Add(line);
        }

        private void CaptureAuthUrl(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var match = AuthUrlRegex.Match(line);
            if (!match.Success)
                return;

            var url = match.Groups["url"].Value.Trim().TrimEnd('.', ',', ';', ')', ']', '"', '\'');
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (string.Equals(_lastAuthUrl, url, StringComparison.Ordinal))
                return;

            _lastAuthUrl = url;
            try { AuthUrlReceived?.Invoke(url); } catch { }
        }

        public async Task WaitForReadyAsync()
        {
            // If socket already exists (daemon running from a prior app launch), use it immediately
            if (!string.IsNullOrEmpty(_socket) && File.Exists(_socket))
                return;

            // If we launched the process ourselves, wait for the socket to appear
            if (!IsRunning)
                throw new InvalidOperationException("tailscaled is not running and socket does not exist.");

            int attempts = 0;
            while (attempts < 30)
            {
                if (File.Exists(_socket))
                    return;

                await Task.Delay(1000);
                attempts++;
            }

            throw new TimeoutException("tailscaled did not create its socket within the expected time.");
        }

        public async System.Collections.Generic.IAsyncEnumerable<JsonNode> WatchIPNBus(int mask = 7, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await WaitForReadyAsync();
            // Long-lived SSE stream — use scoped client to avoid lifetime coupling with Dispose()
            using var client = CreateLocalApiClient();
            using var resp = await client.GetAsync($"/localapi/v0/watch-ipn-bus?mask={mask}", ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
            while (!ct.IsCancellationRequested)
            {
                string line = await reader.ReadLineAsync().WaitAsync(ct);
                if (line == null) yield break;
                if (line.Length == 0) continue;
                JsonNode node = null;
                try { node = JsonNode.Parse(line); } catch { }
                if (node != null) yield return node;
            }
        }

        public async Task StartLoginInteractiveAsync(CancellationToken ct = default)
        {
            await WaitForReadyAsync();
            _localApiClient ??= CreateLocalApiClient();
            var client = _localApiClient;
            using var content = new StringContent("");
            var resp = await client.PostAsync("/localapi/v0/login-interactive", content, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task<JsonNode> GetStatusAsync(CancellationToken ct = default)
        {
            await WaitForReadyAsync();
            _localApiClient ??= CreateLocalApiClient();
            var client = _localApiClient;
            using var resp = await client.GetAsync("/localapi/v0/status", ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            resp.EnsureSuccessStatusCode();
            return JsonNode.Parse(body);
        }

        public async Task<bool> WaitForBackendRunningAsync(int timeoutMs = 10000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var status = await GetStatusAsync(cts.Token);
                    var backendState = status?["BackendState"]?.ToString();
                    TailscaleDebugLog.Add($"WaitForBackendRunningAsync: backendState={backendState}");
                    if (string.Equals(backendState, "Running", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"WaitForBackendRunningAsync error: {ex.Message}");
                }

                try { await Task.Delay(250, cts.Token); } catch { break; }
            }
            return false;
        }

        public void Stop()
        {
            if (_tailscaledProc == null || _tailscaledProc.HasExited)
                return;

            try
            {
                _tailscaledProc.Kill();
                _tailscaledProc.WaitForExit(5000);
            }
            catch
            {
                // Ignore cleanup errors
            }
            finally
            {
                _tailscaledProc?.Dispose();
            }
        }

        private HttpClient CreateLocalApiClient()
        {
            // Use SocketsHttpHandler with Unix socket connect callback
            var handler = new SocketsHttpHandler();
            handler.ConnectCallback = async (context, ct) =>
            {
                TailscaleDebugLog.Add($"Connecting to Unix socket: {_socket}");
                try
                {
                    var socket = new System.Net.Sockets.Socket(
                        System.Net.Sockets.AddressFamily.Unix, 
                        System.Net.Sockets.SocketType.Stream, 
                        System.Net.Sockets.ProtocolType.IP);
                    
                    var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(_socket);
                    await socket.ConnectAsync(endpoint, ct);
                    TailscaleDebugLog.Add("Unix socket connected successfully");
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"Unix socket connect failed: {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            };
            
            var client = new HttpClient(handler);
            client.BaseAddress = new Uri("http://local-tailscaled.sock");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("Sec-Tailscale", "localapi");
            return client;
        }

        [System.Runtime.InteropServices.DllImport("libc")]
        private static extern int chmod(string pathname, int mode);

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _localApiClient?.Dispose();
            _localApiClient = null;
            _disposed = true;
        }

        public static int GetFreePort(int defaultPort)
        {
            // Race condition fix: bind to port 0 (OS assigns free port) and return it.
            // The caller will use this port immediately, so we don't need to keep the socket open.
            // This avoids the TOCTOU race where the port could be grabbed between Stop() and actual use.
            try
            {
                var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            }
            catch
            {
                return defaultPort;
            }
        }

        /// <summary>
        /// Allocates multiple free TCP ports atomically by binding all listeners to port 0 first,
        /// then collecting all assigned ports, then stopping all listeners. Guarantees no two ports
        /// in the same batch collide, since none are released until all are allocated.
        /// </summary>
        public static List<int> GetFreePorts(int count, params int[] defaultPorts)
        {
            var listeners = new List<System.Net.Sockets.TcpListener>();
            var ports = new List<int>(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    listeners.Add(listener);
                    ports.Add(port);
                }
                return ports;
            }
            catch
            {
                // Return defaults for any that failed
                while (ports.Count < count)
                {
                    int idx = ports.Count;
                    int fallback = (defaultPorts != null && idx < defaultPorts.Length) ? defaultPorts[idx] : 0;
                    ports.Add(fallback);
                }
                return ports;
            }
            finally
            {
                foreach (var l in listeners)
                {
                    try { l.Stop(); } catch { }
                }
            }
        }

        private static int GetFreeUdpPort(int defaultPort)
        {
            // Race condition fix: bind to port 0 (OS assigns free port) and return it.
            try
            {
                var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                int port = ((IPEndPoint)socket.LocalEndPoint).Port;
                socket.Close();
                return port;
            }
            catch
            {
                return defaultPort;
            }
        }

        /// <summary>
        /// Allocates multiple free UDP ports atomically by binding all sockets to port 0 first,
        /// then collecting all assigned ports, then closing all sockets. Guarantees no two ports
        /// in the same batch collide.
        /// </summary>
        private static List<int> GetFreeUdpPorts(int count, int defaultPort)
        {
            var sockets = new List<System.Net.Sockets.Socket>();
            var ports = new List<int>(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                    int port = ((IPEndPoint)socket.LocalEndPoint).Port;
                    sockets.Add(socket);
                    ports.Add(port);
                }
                return ports;
            }
            catch
            {
                while (ports.Count < count) ports.Add(defaultPort);
                return ports;
            }
            finally
            {
                foreach (var s in sockets)
                {
                    try { s.Close(); } catch { }
                }
            }
        }
    }
}
