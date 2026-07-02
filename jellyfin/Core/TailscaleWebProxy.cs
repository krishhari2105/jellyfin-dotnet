using System;
using System.Net;

namespace JellyfinTizen.Core
{
    public class TailscaleWebProxy : IWebProxy
    {
        private readonly Uri _proxyUri = new Uri("http://127.0.0.1:3128");

        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            if (IsTailscale(destination))
            {
                TailscaleDebugLog.Add($"Proxy routing: {destination.AbsoluteUri} -> {_proxyUri}");
                return _proxyUri;
            }
            TailscaleDebugLog.Add($"Direct routing (bypass proxy): {destination.AbsoluteUri}");
            return null;
        }

        public bool IsBypassed(Uri destination)
        {
            return !IsTailscale(destination);
        }

        private bool IsTailscale(Uri uri)
        {
            if (uri == null || string.IsNullOrWhiteSpace(uri.Host))
                return false;

            // Route through proxy if tailscale socket/service exists (even if process restarted)
            if (AppState.Tailscale == null)
                return false;

            // Check both: process is running OR it previously ran (socket may still exist)
            bool canProxy = AppState.Tailscale.IsRunning || AppState.Tailscale.IsSocketReachable;
            if (!canProxy)
                return false;

            string host = uri.Host;
            return host.StartsWith("100.") ||
                   host.StartsWith("127.0.") ||
                   host.StartsWith("fd") ||
                   host.Equals("localhost-tailscaled", StringComparison.OrdinalIgnoreCase);
        }
    }
}
