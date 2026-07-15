# Tailscale Integration Setup

## Overview

The Jellyfin Tizen app includes integrated Tailscale VPN support to stream media from tailnet servers. This requires building the `tailscaled` binary and placing it in the app's `lib/` folder.

If you do not want Tailscale support, the project provides a **standalone build flavor** that completely compiles out all Tailscale-specific code and dependencies at compile time.

---

## Build Flavors (Pipelines)

### 1. Tailscale-Enabled Build (Default)
By default, the application is compiled with Tailscale support enabled. This builds the VPN services, includes the QR code auth screens, and bundles the `tailscaled` binaries from the `jellyfin/lib/` folder into the final TPK.

```bash
# Build the default (Tailscale-enabled) TPK
dotnet build -c Release
```
*Note: If no `tailscaled` binary is present in `jellyfin/lib/` at runtime, the application will degrade gracefully and run as a standard local-only client (see **Runtime Fallback Mode** below).*

### 2. Standalone Build (Tailscale Disabled)
If you wish to produce a lightweight Jellyfin Tizen client with no Tailscale binaries, code, or third-party QR code generation packages:

```bash
# Build the standalone (Tailscale-disabled) TPK
dotnet build -c Release -p:TailscaleEnabled=false
```

This configuration:
- Excludes Tailscale-specific source files (`Core/Tailscale*.cs`, `Screens/Tailscale*.cs`, `Utils/QrCodeHelper.cs`).
- Excludes the `QRCoder` NuGet PackageReference.
- Automatically excludes `tailscaled` and `tailscaled-x86` from being packaged inside the TPK.
- Redirects build outputs to `bin-standalone/` and `obj-standalone/` to prevent caching conflicts with Tailscale-enabled builds.

---

## Staging the tailscaled Binaries (Tailscale Flavor Only)

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

---

## Setting up the Jellyfin app (Tailscale Flavor Only)

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

**Note:** Simply copying the binary and reinstalling the old tpk won't show the Tailscale option - you must rebuild the app code first. The app automatically detects which binary (ARM or x86) is present at runtime.

---

## How it works

### Compile-time Exclusions (Standalone)
When `-p:TailscaleEnabled=false` is used, the compilation constant `TAILSCALE` is not defined. The compiler completely bypasses Tailscale-related code via `#if TAILSCALE` guards, and MSBuild compiles out the files entirely.

### Runtime Fallback Mode (Tailscale-Enabled Build)
When compiling the default flavor:
- **Without `tailscaled` binary in `lib/`**: The app starts normally and functions as a regular Jellyfin client. All media streaming works through your local network. The Tailscale settings option is hidden.
- **With `tailscaled` binary in `lib/`**: The app launches `tailscaled` as a subprocess on startup. The local HTTP proxy starts on `127.0.0.1:3128` (or another designated port). When the Jellyfin server is accessed via a Tailscale IP (100.x.y.z, 127.0.y.z, or fdxx), requests are automatically routed through the Tailscale tunnel.

---

## Architecture (Tailscale Flavor Only)

```
jellyfin/
├── lib/
│   └── tailscaled           # ARMv7 binary (must be present for Tailscale support)
├── Core/
│   ├── TailscaleService.cs  # Launches tailscaled subprocess
│   ├── TailscaleProxyService.cs  # Local HTTP reverse proxy
│   ├── TailscaleWebProxy.cs  # Custom proxy routing
│   └── TailscaleConnectionMonitor.cs # VPN connection monitor
├── Screens/
│   ├── TailscaleAuthScreen.cs # Auth QR code setup screen
│   ├── TailscaleScreen.cs     # Connection status display
│   └── VideoPlayerScreen.cs # Rewrites media URLs through proxy when needed
└── App.cs                   # Optional initialization (graceful fallback if binary missing)
```

---

## Troubleshooting (Tailscale Flavor Only)

- **App crashes on startup**: Ensure `tailscaled` binary exists at `jellyfin/lib/tailscaled` with executable permissions (0755)
- **Proxy not working**: Check that Tailscale is running on the TV (companion Tailscale app must be installed and logged in)
- **Media won't play**: Verify the Jellyfin server is accessible via Tailscale IP from another device on your tailnet