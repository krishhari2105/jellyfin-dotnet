# Jellyfin Tizen Client with Tailscale Integration

A native Jellyfin client for Samsung Tizen TVs written in C#/.NET with integrated Tailscale secure networking.

**Important Note**: This project was built with AI assistance.

## Overview

This application provides a Jellyfin media client for Tizen TVs with built-in Tailscale VPN support for secure remote access to your media server.

## Project Structure

```
jellyfin-dotnet/
├── jellyfin/                 # Main application (.NET 6.0 Tizen app)
│   ├── App.cs                # Application entry point
│   ├── Core/                 # Core services (AppState, Tailscale, Jellyfin, etc.)
│   ├── Models/               # Data models
│   ├── Screens/              # UI screens (including Tailscale authentication)
│   ├── UI/                   # UI components
│   └── Utils/                # Utility classes
├── tailscale-tizen/          # Build scripts for Tailscale binaries

```

## Key Features

- Native Tizen TV application using .NET/C#
- Full Jellyfin client functionality (browse libraries, play media)
- Integrated Tailscale VPN for secure remote access

## Building

1. Build Tailscale binaries for ARM (real TV) and x86 (emulator) and place in `jellyfin/lib/`
   - `tailscaled` (ARM binary)
   - `tailscaled-x86` (x86 binary)
2. Build the .NET application:
   ```bash
   dotnet build -c Release
   ```
3. Package for Tizen:
   ```bash
   tizen package -t tpk -c Release
   ```

## Usage

1. Install the generated TPK on your Tizen TV
2. Launch the app and navigate to Tailscale settings
3. Connect via QR code or manual URL
4. Configure your Jellyfin server using the Tailscale IP address

## Credits

- Built with AI assistance (primarily Codex)
- Uses [Tizen.NET](https://github.com/Samsung/Tizen.NET) and [QRCoder](https://github.com/codebude/QRCoder)
- Integrates [Tailscale](https://github.com/tailscale/tailscale) for secure networking

## Disclaimer

This is a Self-developed project using AI. Not officially affiliated with Jellyfin, Tailscale, or Samsung.
