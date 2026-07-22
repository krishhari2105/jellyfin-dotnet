# CLAUDE.md

This file provides guidance to Claude Code when working with the Jellyfin Tizen TV client repository.

## Project Overview

A native Jellyfin media client for Samsung Tizen TVs using .NET 6.0 and Tizen.NET (NUI).

**Key Architecture**: Two build flavors controlled by `TailscaleEnabled` MSBuild property (default `true`):
- **Standard Build**: Full Tailscale VPN integration (daemon + proxy + auth screens)
- **Standalone Build** (`-p:TailscaleEnabled=false`): All Tailscale code compiled out via `Compile Remove` in csproj

## Build System

### Standard (Tailscale Enabled)
```bash
dotnet build -c Release
```
Artifacts: `bin/Release/net6.0-tizen9.0/`

### Standalone (Tailscale Disabled)
```bash
dotnet build -c Release -p:TailscaleEnabled=false
```
Artifacts: `bin-standalone/Release/net6.0-tizen9.0/`

**Critical**: Output paths are separated via `Directory.Build.props` to prevent cache conflicts between flavors.

## Code Architecture

### Application Lifecycle (`jellyfin/`)
- **Entry Point**: `App.cs` → `NUIApplication` subclass. Initializes `AppState` (services) and `NavigationService` (screen stack), navigates to `StartupScreen`.
- **Global State**: `Core/AppState.cs` — singleton hub. Holds `HttpClient`, `JellyfinService`, `TailscaleService`, `TailscaleProxyService`, server registry, session state, device ID. All static properties.
- **Navigation**: `Core/NavigationService.cs` — manages screen stack, key handling, loading overlays, exit confirmation.
- **UI Base**: `Screens/ScreenBase.cs` — all screens inherit this. Provides `RunOnUiThread()`, `FireAndForget()`, timer/animation disposal helpers, debug overlay.

### Core Services

| Service | Purpose |
|---------|---------|
| `AppState` | Global registry, session persistence, server management, URL rewriting for Tailscale |
| `JellyfinService` | All Jellyfin API calls, circuit breaker (5 failures = 10s block), retry (3 attempts, 500ms backoff) |
| `TailscaleService` | Manages `tailscaled` userspace daemon (ARM/x86 binaries from `jellyfin/lib/`), Unix socket API |
| `TailscaleProxyService` | Local HTTP proxy (`HttpListener` on 127.0.0.1:8123) routing Tailscale IPs (100.x, 127.0.x, fd*) through daemon |
| `TailscaleConnectionMonitor` | Background task checking tailnet connectivity every 10s, auto-reconnect |
| `TailscaleWebProxy` | `IWebProxy` implementation — routes Tailscale-destined HTTP through local proxy |

### Screens (`Screens/`)
All screens inherit `ScreenBase`, implement `IKeyHandler` for remote input.

| Screen | Purpose |
|--------|---------|
| `StartupScreen` | Boot flow: restore session → Tailscale auth (if needed) → server picker → user select → home |
| `ServerSetupScreen` | Add new server (HTTPS-first probe, Emby/Jellyfin detection) |
| `ServerPickerScreen` | Horizontal carousel of saved servers, add/remove/tailscale actions |
| `UserSelectScreen` | Avatar grid, fetches public users |
| `PasswordScreen` | Password entry with IME configuration |
| `HomeScreen` | Row-based UI: Libraries, Next Up, Continue Watching, Recently Added. **"Server is truth"** — re-fetches Continue Watching on every `OnShow` |
| `HomeLoadingScreen` | Async load of libraries + rows, LRU image URL cache (2500 entries) |
| `LibraryMoviesGridScreen` | Virtualized grid with batched row building, poster lazy-load/unload based on viewport |
| `MovieDetailsScreen` / `EpisodeDetailsScreen` / `SeriesDetailsScreen` / `SeasonDetailsScreen` | Shared `DetailsScreenBase` for metadata, action buttons, subtitle/audio/source selection panels |
| `VideoPlayerScreen` | Tizen `TVView` playback, trickplay, subtitle rendering, play method selection (DirectPlay/DirectStream/Transcode) |
| `TailscaleScreen` / `TailscaleAuthScreen` | QR-code login, status polling, daemon management |

### UI System (`UI/`)
- `UiTheme.cs` — centralized colors, spacing, typography constants
- `MediaCardFactory` — creates poster cards (image + text + played badge + progress bar)
- `MediaCardFocus` — focus animation (scale + border glow) via `CardFrame` child lookup
- `AppleTvLoadingVisual` — persistent spinner singleton reused across transitions
- `NavigationService` loading overlay — same singleton spinner, raised/reused not recreated
- `MonochromeAuthFactory` — auth flow UI (inputs, buttons, panels)
- `DetailsSelectionPanel` — audio/subtitle/source pickers in details screens

### Utilities (`Utils/`)
- `CacheHelper` — thread-safe LRU+TTL cache (used by `JellyfinService` for Users, CurrentUser, PublicUsers)
- `UiAnimator` — animation lifecycle management (`Start`, `AnimateTo`, `StopAndDispose`, `Replace`)
- `JellyfinImageUrlBuilder` — builds image URLs with Tailscale rewrite
- `QrCodeHelper` — generates QR codes to file for Tailscale auth
- `PerfTrace` — `Start()`/`End(label)` for debug timing (guarded by `DebugSwitches`)

## Mandatory Coding Patterns

### 1. UI Thread Marshaling
```csharp
Tizen.Applications.CoreApplication.Post(() => { /* UI mutation */ });
```
**Every** UI mutation must use this. `ScreenBase.RunOnUiThread()` wraps it.

### 2. Resource Cleanup in `OnHide()`
```csharp
public override void OnHide()
{
    _timer?.Dispose();
    _animation?.Stop();
    _cts?.Cancel();
    base.OnHide(); // calls HideDebugOverlay()
}
```
**Failure = memory leaks on Tizen.** Every screen must dispose timers, stop animations, cancel tokens.

### 3. Shared HttpClient
**Never** `new HttpClient()` in a method. Use `AppState.HttpClient` (created once in `AppState.Init()` with `TailscaleWebProxy` handler when Tailscale enabled).

### 4. Tailscale Conditional Compilation
```csharp
#if TAILSCALE
// Tailscale-only code
#endif
```
Check `jellyfin.csproj` `Compile Remove` list before referencing Tailscale types in shared code.

### 5. Async Patterns
- `async void` **only** for Tizen event handlers
- Everywhere else: `Task` + proper exception handling
- `FireAndForget(Task, string)` in `ScreenBase` logs unhandled exceptions

### 6. Navigation
- `NavigationService.Navigate(screen, addToStack)` — forward
- `NavigationService.NavigateBack()` — pop stack
- `NavigationService.NavigateWithLoading(factory, message)` — shows persistent spinner, navigates when ready
- `NavigationService.ShowLoadingOverlay(message)` / `HideLoadingOverlay()` — in-place spinner (singleton `AppleTvLoadingVisual`)

## Tailscale Integration Details

### Binary Requirements
Place in `jellyfin/lib/`:
- `tailscaled` (ARM64 for real TV)
- `tailscaled-x86` (x86 for emulator)

### Daemon Startup (`TailscaleService.StageAndStart()`)
1. Copies correct binary to app data dir, `chmod 755`
2. Kills orphaned `tailscaled` processes
3. Allocates dynamic ports (P2P UDP, HTTP proxy, SOCKS5) via port-0 bind
4. Starts with `--tun=userspace-networking`, `--socks5-server`, `--outbound-http-proxy-listen`
5. Waits for Unix socket to appear (`WaitForReadyAsync`)

### Proxy Service (`TailscaleProxyService`)
- `HttpListener` on `127.0.0.1:8123` (dynamic port)
- `/proxy?url=` endpoint
- **Image caching**: In-memory LRU (32MB, 100 entries) + persistent disk cache (120MB, 30-day TTL, SHA256 keyed)
- **Cache key normalization**: Strips `api_key`, `X-Emby-Token`, `ApiKey` query params so images cache across sessions
- Video/audio: streamed directly (chunked), never cached

### URL Rewriting
`AppState.RewriteImageUrlForTailscale(url)` — if URL host is Tailscale IP (100.x, 127.0.x, fd*, localhost-tailscaled), rewrites to `http://127.0.0.1:8123/proxy?url=<escaped>`.

## Critical Files to Understand First

1. `jellyfin/App.cs` — boot sequence
2. `jellyfin/Core/AppState.cs` — global state machine, session persistence, Tailscale lifecycle
3. `jellyfin/Core/JellyfinService.cs` — API client, circuit breaker, retry, playback info logic
4. `jellyfin/Core/NavigationService.cs` — screen stack, key routing, loading overlay singleton
5. `jellyfin/Screens/ScreenBase.cs` — base class, UI thread helpers, cleanup pattern
6. `jellyfin/Screens/VideoPlayerScreen.cs` — playback pipeline (400+ lines in `StartPlaybackAsync`, technical debt flag)
7. `jellyfin/Screens/DetailsScreenBase.cs` — shared details logic (metadata, action buttons, selection panels)
8. `jellyfin/UI/MediaCardFactory.cs` — card creation, text height estimation
9. `jellyfin/Utils/UiAnimator.cs` — animation management

## Common Tasks

### Add a Screen
1. Create `Screens/NewScreen.cs` inheriting `ScreenBase`
2. Implement `IKeyHandler.HandleKey(AppKey)`
3. Override `OnShow()` / `OnHide()` — **cleanup in OnHide**
4. Use `NavigationService.Navigate(new NewScreen())`

### Add API Call
1. Add method to `JellyfinService.cs`
2. Use `ExecuteWithRetryAsync` or `GetAsync`/`PostAsync` helpers
3. Handle `UnauthorizedDetected` event (clears session, returns to `StartupScreen`)

### Add Dependency
- Edit `jellyfin.csproj`
- If Tailscale-only: wrap `PackageReference` in `Condition=" '$(TailscaleEnabled)' == 'true' "`
- If used in shared code: guard with `#if TAILSCALE`

### Debug Logging
```csharp
TailscaleDebugLog.Add("message"); // gated by DebugSwitches.EnableVerboseDebugLogging
```
Enable `DebugSwitches.EnableVerboseDebugLogging = true` and/or `DebugSwitches.EnablePlaybackDebugOverlay = true` for on-screen debug overlay.

## Testing / Deployment

- **No unit test project exists** — verify on Tizen emulator or physical TV via `sdb` deploy to device
- **TPK output**: `bin/**/org.tizen.jellyfin.dotnet-2.0.0.tpk`
- Install via Tizen CLI or [Apps2Samsung](https://github.com/Apps2Samsung/Apps2Samsung)

## Known Technical Debt (Verified Status)

| Debt Item | Status | Evidence |
|-----------|--------|----------|
| `VideoPlayerScreen.StartPlaybackAsync()` decomposition | ✅ **FIXED** | `StartPlaybackAsync()` now gates startup then delegates to request creation, playback-plan negotiation, and native-player preparation; both Release flavors build successfully. |
| `MovieDetailsScreen` / `EpisodeDetailsScreen` duplication | ✅ **FIXED** (Movie/Episode) | Both inherit `DetailsScreenBase` (1230 lines) consolidating: action buttons, selection panels, media source management, metadata UI, overview scrolling, resume reconciliation. |
| `SeasonDetailsScreen` duplication | ✅ **FIXED** | Kept composition (the season carousel is not a playback-details screen) while sharing overview measurement, card-focus rendering, and carousel positioning with the related details screen. |
| `CacheHelper` size-based eviction | ✅ **FIXED** | Adds opt-in caller-supplied byte estimates, `MaxBytes`, atomic byte/LRU accounting, and focused tests for eviction, replacement, oversized entries, and clearing. |
| Hardcoded timeouts/buffer sizes | ✅ **FIXED** | API timeout, Jellyfin retry/circuit policy, proxy readiness, resume retry policy, and player prepare timeout are centralized in `AppState` with existing defaults preserved. |

## Critical Reminders
- **Tizen kills background apps** — no long-running background tasks survive suspend
- **Socket exhaustion** — reuse `HttpClient`, dispose streams properly
- **Memory is tight** — unload offscreen images (`LibraryMoviesGridScreen` does this)
- **UI thread only** — NUI is not thread-safe, always `CoreApplication.Post`
