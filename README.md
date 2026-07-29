# Jellyfin Tizen Client

> [!IMPORTANT]
> **Archived — no longer maintained**
>
> Development of this project has ended, and no further fixes, features, releases,
> or support are planned. The public Tizen .NET APIs do not expose several TV
> capabilities needed by a complete media client, including reliable access to
> remote-control media keys through the TV input-device APIs. Third-party .NET
> applications therefore cannot reach the same platform integration available to
> Tizen web applications or privileged proprietary OTT runtimes.
>
> Future effort is moving to [Litefin](https://github.com/Samsung/litefin), a
> lightweight JavaScript Jellyfin client with broader Tizen feature support.
> This repository remains available as a historical reference and a personal
> record of the work that went into it.

A native Jellyfin media client for Samsung Tizen TVs, written in C#/.NET with Tizen NUI.

> This project was built with AI assistance.

## Features

- Browse Jellyfin libraries, recently added media, next-up episodes, and continue-watching items.
- Play direct, remuxed, and transcoded media through the native Tizen player.
- Select audio tracks, subtitles, media sources, and playback display modes.
- Save and switch between multiple Jellyfin or Emby-compatible servers.
- Navigate the entire application with a TV remote.

## Project structure

```text
jellyfin-dotnet/
├── jellyfin/                   # Main .NET 6 Tizen application
│   ├── App.cs                  # Application entry point
│   ├── Core/                   # State, Jellyfin API, navigation, playback profiles
│   ├── Models/                 # Application and API models
│   ├── Screens/                # Full-screen NUI views
│   ├── UI/                     # Theme and reusable UI components
│   ├── Utils/                  # Caching, animation, image, and performance helpers
│   └── shared/res/             # Application icons and image assets
└── jellyfin.CacheHelper.Tests/ # Unit tests for framework-neutral core helpers
```

## Building

Prerequisites:

- A supported .NET SDK.
- The Samsung Tizen .NET workload.
- A Tizen signing profile when producing an installable package.

Restore and build the application:

```powershell
dotnet restore jellyfin.sln
dotnet build jellyfin/jellyfin.csproj -c Release
```

The installable package is generated under:

```text
jellyfin/bin/Release/net6.0-tizen9.0/
```

Run the unit tests with:

```powershell
dotnet test jellyfin.CacheHelper.Tests/jellyfin.CacheHelper.Tests.csproj
```

## Installation

Install the generated `.tpk` through Tizen Studio, `sdb install <file>.tpk`, or a compatible deployment tool such as [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung). Launch the app and enter the URL of a reachable Jellyfin server.

## Credits

- [Jellyfin](https://jellyfin.org/)
- [Tizen.NET](https://github.com/Samsung/Tizen.NET)
- [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung)
- [litefin](https://github.com/Samsung/litefin)

## Disclaimer

This is an independent project and is not officially affiliated with Jellyfin or Samsung.
