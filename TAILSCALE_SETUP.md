# Tailscale Integration Setup

## Overview

The Jellyfin Tizen app includes optional Tailscale integration to stream media from tailnet servers. This requires building the `tailscaled` binary and placing it in the app's `lib/` folder.

## Building tailscaled

### Option 1: Use pre-built official Tailscale static binaries (recommended for testing)

Download the official Tailscale static binaries from: https://github.com/tailscale/tailscale/releases

For **x86 emulator** (64-bit):
```bash
# Extract and copy the x86_64 binary
cp path/to/tailscaled_1.75.0_amd64/tailscaled jellyfin/lib/tailscaled-x86
chmod +x jellyfin/lib/tailscaled-x86
```

For **ARM TV** (32-bit ARMv7):
```bash
# Extract and copy the ARM32 binary if available, or use make:
cd tailscale-tizen && make tailscaled
cp tailscale-tizen/Tailscale/lib/tailscaled jellyfin/lib/
```

### Option 2: Build from source using Make

### For real Tizen TV (ARMv7)

```bash
cd tailscale-tizen
make tailscaled
```

This cross-compiles `tailscaled` for ARMv7 and outputs it to `tailscale-tizen/Tailscale/lib/tailscaled`.

### For Tizen emulator (x86)

```bash
cd tailscale-tizen
make tailscaled-x86
```

This cross-compiles `tailscaled` for x86 and outputs it to `tailscale-tizen/Tailscale/lib/tailscaled-x86`.

## Setting up the Jellyfin app

**Important:** After copying the binary, you must rebuild the Jellyfin app for the Tailscale UI to appear.

### For real TV (ARM):

1. Copy the ARM binary:
   ```bash
   cp tailscale-tizen/Tailscale/lib/tailscaled jellyfin/lib/
   ```

### For emulator (x86):

1. Copy the x86 binary:
   ```bash
   cp tailscale-tizen/Tailscale/lib/tailscaled-x86 jellyfin/lib/
   ```

2. Rebuild the Jellyfin app:
   ```bash
   cd jellyfin
   dotnet build
   ```

3. Package and install the updated app:
   ```bash
   # Follow your normal Tizen packaging/signing process
   # The tpk size should increase by ~9MB with the binary included
   ```

**Note:** Simply copying the binary and reinstalling the old tpk won't show the Tailscale option - you must rebuild the app code first. The app automatically detects which binary (ARM or x86) is present.

## How it works

- **Without `tailscaled` binary**: The app starts normally and functions as a regular Jellyfin client. All media streaming works through your local network.
- **With `tailscaled` binary**: The app launches `tailscaled` as a subprocess on startup. The local HTTP proxy starts on `127.0.0.1:8123`. When the Jellyfin server is accessed via a Tailscale IP (100.x.y.z, 127.0.y.z, or fdxx), requests are automatically routed through the Tailscale tunnel.

## Architecture

```
jellyfin/
├── lib/
│   └── tailscaled           # ARMv7 binary (must be present for Tailscale support)
├── Core/
│   ├── TailscaleService.cs  # Launches tailscaled subprocess
│   └── TailscaleProxyService.cs  # Local HTTP reverse proxy
├── Screens/
│   └── VideoPlayerScreen.cs # Rewrites media URLs through proxy when needed
└── App.cs                   # Optional initialization (graceful fallback if binary missing)
```

## Troubleshooting

- **App crashes on startup**: Ensure `tailscaled` binary exists at `jellyfin/lib/tailscaled` with executable permissions (0755)
- **Proxy not working**: Check that Tailscale is running on the TV (companion Tailscale app must be installed and logged in)
- **Media won't play**: Verify the Jellyfin server is accessible via Tailscale IP from another device on your tailnet