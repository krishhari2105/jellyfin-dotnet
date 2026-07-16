# Jellyfin Tizen Client with Tailscale Integration

A native Jellyfin client for Samsung Tizen TVs written in C#/.NET with optional integrated Tailscale secure networking.

**Important Note**: This project was built with AI assistance.

## Overview

This application provides a Jellyfin media client for Tizen TVs. It supports two build flavors:
1. **Tailscale-Enabled (Default)**: Includes built-in Tailscale VPN support for secure remote access to your media server.
2. **Standalone**: A lightweight configuration that compiles out all Tailscale-specific code, screens, and dependencies.

## Project Structure

```
jellyfin-dotnet/
├── jellyfin/                 # Main application (.NET 6.0 Tizen app)
│   ├── App.cs                # Application entry point
│   ├── Core/                 # Core services (AppState, Jellyfin, Navigation, and conditional Tailscale)
│   ├── Models/               # Data models
│   ├── Screens/              # UI screens (including details, playback, settings, and conditional Tailscale auth)
│   ├── UI/                   # UI styling and factory components
│   └── Utils/                # Utility classes (animations, image builders)
├── tailscale-tizen/          # Build scripts for Tailscale binaries
```

## Key Features

- Native Tizen TV application using .NET/C#
- Full Jellyfin client functionality (browse libraries, play media)
- Optional integrated Tailscale VPN for secure remote access

## Building

### Prerequisites

1. Install the Tizen .NET workload (requires .NET 6 SDK):
   ```bash
   Invoke-WebRequest "https://raw.githubusercontent.com/Samsung/Tizen.NET/main/workload/scripts/workload-install.ps1" -OutFile "workload-install.ps1"

   .\workload-install.ps1
   ```

### 1. Tailscale-Enabled Flavor (Default)
This flavor includes VPN services and bundles the `tailscaled` binary.
1. Build or download Tailscale binaries for ARM (real TV) and x86 (emulator) from "https://tailscale.com/kb/1053/install-static" and place them in `jellyfin/lib/`:
   - `tailscaled` (ARM binary)
   - `tailscaled-x86` (x86 binary)
2. Build the application:
   ```bash
   dotnet build -c Release
   ```
   *Artifacts are written to `bin/Release/net6.0-tizen9.0/`.*

### 2. Standalone Flavor (Tailscale Disabled)
This flavor compiles out all Tailscale-related code and packages without bundling any VPN binaries.
1. Build the application:
   ```bash
   dotnet build -c Release -p:TailscaleEnabled=false
   ```
   *Artifacts are written to `bin-standalone/Release/net6.0-tizen9.0/`.*

## Usage

1. Install the generated TPK on your Tizen TV (refer to official Tizen documentation for installing TPK files on Tizen TVs) or use [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung) for easier installation.
2. Launch the app and configure your Jellyfin server.
3. If using the Tailscale flavor:
   - Navigate to Tailscale settings
   - Connect via QR code or manual URL
   - Configure your Jellyfin server using the Tailscale IP address

## Credits

- Built with AI assistance
- Uses [Tizen.NET](https://github.com/Samsung/Tizen.NET) and [QRCoder](https://github.com/codebude/QRCoder) (for Tailscale)
- Integrates [Tailscale](https://github.com/tailscale/tailscale) for secure networking
- [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung)
- [litefin](https://github.com/Samsung/litefin)

## Disclaimer

This is a self-developed project using AI. Not officially affiliated with Jellyfin, Tailscale, or Samsung.
