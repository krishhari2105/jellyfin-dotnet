# GEMINI.md

This file provides guidance to Gemini agents and IDEs when working with code in this repository.

## Project Overview

This is a **Jellyfin Tizen TV client with integrated Tailscale VPN support**, built with C#/.NET 6.0 targeting Samsung Tizen TVs. The app provides full Jellyfin client functionality (browse libraries, play media) with optional built-in Tailscale secure networking for remote access.

**Key technologies**: .NET 6, Tizen.NET, NUI (UI framework), Tailscale (userspace VPN), QRCoder.

---

## Build Commands

### Prerequisites
- .NET 6 SDK
- Tizen .NET workload (see README.md for installation script)

### Standard Build (Tailscale Enabled)
By default, the application builds with Tailscale VPN integration included.
```bash
# Debug build (outputs to bin/Debug/net6.0-tizen9.0)
dotnet build -c Debug

# Release build for TPK generation (outputs to bin/Release/net6.0-tizen9.0)
dotnet build -c Release
```

### Standalone Build (Tailscale Disabled)
To build a lightweight, standalone Jellyfin client without any Tailscale code, screens, or dependencies:
```bash
# Debug build (outputs to bin-standalone/Debug/net6.0-tizen9.0)
dotnet build -c Debug -p:TailscaleEnabled=false

# Release build for TPK generation (outputs to bin-standalone/Release/net6.0-tizen9.0)
dotnet build -c Release -p:TailscaleEnabled=false
```

### Running
- The app builds a `.tpk` package installed on Tizen TV.
- Use `sdb` or [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung) for deployment.
- No local "run" command - development requires Tizen emulator or physical TV.

---

## Architecture Overview

### Core Services (`jellyfin/Core/`)
| Service | Purpose | Key Files |
|---------|---------|-----------|
| **AppState** | Global singleton state, session management, server registry | `AppState.cs` |
| **JellyfinService** | API client for Jellyfin server (libraries, playback, images) | `JellyfinService.cs` |
| **TailscaleService** | *[Tailscale-only]* Manages `tailscaled` daemon lifecycle, auth, status | `TailscaleService.cs` |
| **TailscaleProxyService** | *[Tailscale-only]* Local HTTP proxy for routing Tailscale IPs through tailscaled | `TailscaleProxyService.cs` |
| **TailscaleWebProxy** | *[Tailscale-only]* Custom `IWebProxy` implementation | `TailscaleWebProxy.cs` |
| **TailscaleConnectionMonitor** | *[Tailscale-only]* Monitors VPN status and network changes | `TailscaleConnectionMonitor.cs` |
| **NavigationService** | Screen stack management, UI thread marshaling, page transitions | `NavigationService.cs` |
| **CacheHelper** | Simple in-memory cache with TTL | `CacheHelper.cs` |

### Screens (`jellyfin/Screens/`)
Screens inherit from `ScreenBase` (which inherits `Tizen.NUI.BaseComponents.View`).
- **Lifecycle**: `OnShow()` → visible, `OnHide()` → hidden (must clean up timers/animations)
- **Navigation**: `NavigationService.Navigate(screen, addToStack)` / `NavigateBack()`
- **Key handling**: Implement `IKeyHandler.HandleKey(AppKey)`
- **Tailscale Screens** (`TailscaleAuthScreen.cs` and `TailscaleScreen.cs`) are compiled out in the Standalone build.

### UI Patterns
- **Factory methods**: `UiFactory.CreateAtmosphericBackground()`, `MonochromeAuthFactory.CreateInputFieldShell()`
- **Loading visuals**: `AppleTvLoadingVisual` for async operations
- **Debug overlay**: `ScreenBase.CreateDebugOverlay()` / `ShowDebugOverlayPublic()` (enabled via `DebugSwitches`)

---

## Critical Patterns & Conventions

### 1. Async Void for UI Event Handlers
```csharp
public async void OnAppResumed() { ... } // OK - event handler
```
But avoid `async void` elsewhere; use `Task` with proper error handling.

### 2. UI Thread Marshaling
```csharp
Tizen.Applications.CoreApplication.Post(() => { /* UI work */ });
```
All UI mutations MUST go through `CoreApplication.Post()`.

### 3. Timer/Animation Cleanup
Every screen **MUST** dispose timers/animations in `OnHide()`:
```csharp
public override void OnHide()
{
    _timer?.Dispose();
    _animation?.Stop();
    base.OnHide();
}
```
Use `ScreenBase.DisposeTimer(ref timer)` helper.

### 4. HTTP Client Reuse
- **AppState** creates single shared `HttpClient` with `TailscaleWebProxy` (if Tailscale is enabled)
- **TailscaleService** uses single `_localApiClient` for Unix socket calls
- **ServerSetupScreen** uses single `_probeHttpClient`
- **NEVER** create `HttpClient` per request (socket leaks on TV)

### 5. Tailscale URL Detection
Single source of truth: `AppState.IsTailscaleUrl(url)`
```csharp
// Checks: 100.x.x.x, 127.0.x.x, fd* (ULA), localhost-tailscaled
#if TAILSCALE
if (AppState.IsTailscaleUrl(serverUrl)) { ... }
#endif
```

### 6. Static Caches Need Eviction
- `TailscaleDebugLog` - event subscriptions must unsubscribe in `OnHide()`
- `HomeLoadingScreen.ImageUrlCache` - LRU with 2500 max entries (implemented)
- `CacheHelper` - TTL-based, no size limit (known issue)

### 7. Conditional Compilation & Build Isolation
The project supports separate compilation configurations to support standalone and Tailscale-enabled flavors:
- **Redirection**: Standalone builds redirect base output pathways to `bin-standalone/` and `obj-standalone/` inside [Directory.Build.props](file:///d:/Apps/github-repos/jellyfin-dotnet/Directory.Build.props) to prevent compiler cache conflicts.
- **Conditional Source Exclusions**: When `TailscaleEnabled` is set to `false`, Tailscale-only source files and the `QRCoder` PackageReference are excluded at compile time, and `tailscaled`/`tailscaled-x86` are removed from the TPK.
- **C# Conditional Guards**: Use `#if TAILSCALE` block directives in shared source files when referring to Tailscale components or configurations.

---

## Common Development Tasks

### Adding a New Screen
1. Create `NewScreen.cs` inheriting `ScreenBase`, implement `IKeyHandler` if needed.
2. Implement `OnShow()` / `OnHide()` with proper cleanup.
3. Navigate via `NavigationService.Navigate(new NewScreen())`.
4. If the screen is Tailscale-only, ensure it is added to the `<Compile Remove="..." />` list in `jellyfin.csproj` when `TailscaleEnabled != true`.

### Modifying Jellyfin API Calls
- Add methods to `JellyfinService.cs`
- Use `WithTimeout(task, ms)` for network calls
- Handle `HttpRequestException` for auth errors (401/403)

### Tailscale Integration Changes *(Tailscale Flavor Only)*
- Daemon management: `TailscaleService.StageAndStart()`, `Stop()`
- Auth flow: `TailscaleAuthScreen` (QR code), `TailscaleScreen` (status)
- Proxy: `TailscaleProxyService.Start()` / `Stop()`, auto-started via `AppState`

### Debug Logging
```csharp
#if TAILSCALE
TailscaleDebugLog.Add("message"); // Static, enables debug overlay (if compiled)
#endif
DebugSwitches.EnableVerboseDebugLogging = true; // Global flag
```

---

## Known Issues (from CODEBASE_ANALYSIS.md)

### Critical (fixed in recent commits)
- ✅ TailscaleReadyTask exception swallowing (Issue #1)
- ✅ TailscaleProxyService HttpListener + HttpClient disposal (Issue #2)
- ✅ TailscaleService blocking .Wait() on UI thread (Issue #3)
- ✅ JellyfinService HttpClient disposal (Issue #4)
- ✅ VideoPlayerScreen timer/animation leaks (Issue #5)
- ✅ TailscaleDebugLog event leak (Issue #6)
- ✅ HomeLoadingScreen unbounded cache (Issue #7)
- ✅ OnAppResumed race conditions (Issue #8)
- ✅ HttpClient reuse in TailscaleService/ServerSetupScreen (Issues #9, #15)
- ✅ HTTP timeout increased to 20s (Issue #14)
- ✅ Tailscale URL detection consolidated (Issue #10)

### Remaining Moderate
- VideoPlayerScreen.StartPlaybackAsync() - 400+ lines, needs extraction
- MovieDetailsScreen / EpisodeDetailsScreen - ~80% duplicate, need base class
- JellyfinService - no retry logic for transient failures
- CacheHelper - no size limit/eviction
- Hardcoded timeouts/buffer sizes need configurability

---

## Key Files to Understand First

1. **`AppState.cs`** - Global state, all services, session management
2. **`NavigationService.cs`** - Screen stack, UI thread, key handling, transitions
3. **`JellyfinService.cs`** - All server API communication
4. **`TailscaleService.cs`** *(Tailscale-only)* - Daemon lifecycle, auth, status polling
5. **`VideoPlayerScreen.cs`** - Largest screen, playback, subtitles, trickplay
6. **`ServerPickerScreen.cs`** / **`ServerSetupScreen.cs`** - Server connection flow

---

## Tizen-Specific Notes

- **No tun device**: Tailscale runs in userspace mode (netstack)
- **tailscaled binary**: Must be bundled in `jellyfin/lib/` (ARM + x86) (Tailscale-only flavor)
- **UI Framework**: Tizen.NUI (not MAUI/Xamarin.Forms)
- **App lifecycle**: `App.OnCreate()` → `OnPause()` → `OnResume()` → `OnTerminate()`
- **Background limits**: No true background execution on Tizen TV

---

## Debug/Development Tips

- Use `TailscaleDebugLog` + `DebugSwitches.EnableVerboseDebugLogging` for runtime logs (when Tailscale is enabled)
- `DebugSwitches.EnablePlaybackDebugOverlay` enables video player debug UI
- `PerfTrace.Start()/End()` for measuring async operations
- `CoreApplication.Post()` for debugging UI thread issues
- Emulator vs device: x86 vs ARM tailscaled binaries in `jellyfin/lib/`

---

## File Structure Quick Reference

```
jellyfin/
├── App.cs                    # Entry point, lifecycle
├── Core/
│   ├── AppState.cs           # Global state (READ FIRST)
│   ├── JellyfinService.cs    # API client
│   ├── TailscaleService.cs   # Daemon management (Tailscale-only)
│   ├── TailscaleProxyService.cs # Local proxy (Tailscale-only)
│   ├── TailscaleWebProxy.cs  # Custom proxy routing (Tailscale-only)
│   ├── TailscaleConnectionMonitor.cs # Connection monitor (Tailscale-only)
│   ├── NavigationService.cs  # Screen navigation
│   └── CacheHelper.cs        # In-memory cache
├── Screens/
│   ├── ScreenBase.cs         # Base class (View + IKeyHandler)
│   ├── StartupScreen.cs      # Boot, Tailscale wait
│   ├── ServerPickerScreen.cs # Server selection
│   ├── HomeScreen.cs         # Main library browser
│   ├── VideoPlayerScreen.cs  # Playback (largest file)
│   ├── TailscaleAuthScreen.cs # Auth QR code setup (Tailscale-only)
│   ├── TailscaleScreen.cs     # Connection status display (Tailscale-only)
│   └── ... (details, settings, password)
├── UI/                       # Reusable UI components
└── Utils/                    # Helpers (PerfTrace, QrCodeHelper [Tailscale-only], etc.)
```
