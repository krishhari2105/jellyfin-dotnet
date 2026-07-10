# Jellyfin Tizen Client Codebase Analysis Report

## Summary
This is a C#/.NET 6.0 Tizen TV application with integrated Tailscale VPN support. The codebase is substantial (~30+ C# files, ~15,000 lines) with significant AI-generated patterns. Overall architecture is functional but contains several stability and maintainability issues critical for a long-running Smart TV app.

---

## CRITICAL Issues (Crashes, Data Loss, Memory Leaks)

### 1. [CRITICAL] File: `Core/AppState.cs` | Lines: 81-115
**Issue:** `TailscaleReadyTask` fire-and-forget task swallows exceptions during startup
```csharp
TailscaleReadyTask = System.Threading.Tasks.Task.Run(async () => {
    try {
        Tailscale.StageAndStart();
        // ...
    } catch (Exception ex) {
        Tizen.Log.Warn("AppState", $"Tailscale not available: {ex.Message}");
    }
});
```
**Why it matters:** If `Tailscale.StageAndStart()` throws before the try block or during `await`, the exception is lost. App starts with broken Tailscale state silently.
**Fix:** Add outer try/catch around `Task.Run`, log task status on completion, consider `TaskScheduler.UnobservedTaskException`.

---

### 2. [CRITICAL] File: `Core/TailscaleProxyService.cs` | Lines: 95-147, 221-250
**Issue:** `HttpListener` started on background thread without proper synchronization; `_forwardClient` (HttpClient) never disposed
```csharp
public void Start() {
    _listener = new HttpListener();
    _listener.Prefixes.Add($"http://{LocalProxyAddress}:{LocalProxyPort}/");
    _listener.Start();
    _cts = new CancellationTokenSource();
    _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token)); // Fire-and-forget
}
```
**Why it matters:** 
- `HttpListener` on non-UI thread can cause race conditions with NUI
- `_forwardClient` (HttpClient) created in constructor but never disposed → socket leaks
- No `using` on `_listener` in `Stop()` → handle leak
**Fix:** Make `Start()` async, use `using` for `_forwardClient`, implement proper `IDisposable` pattern with `DisposeAsync`.

---

### 3. [CRITICAL] File: `Core/TailscaleService.cs` | Lines: 194-260
**Issue:** Blocking `.Wait()` on UI thread during startup
```csharp
try {
    Task.Delay(2000).Wait(); // BLOCKS THREAD
} catch (Exception ex) { ... }
```
**Why it matters:** On Tizen TV, blocking the thread that starts `tailscaled` can deadlock process startup or cause ANR (Application Not Responding).
**Fix:** Make `StageAndStart()` return `Task`, use `await Task.Delay(2000)` throughout.

---

### 4. [CRITICAL] File: `Core/JellyfinService.cs` | Lines: 1057-1070
**Issue:** `HttpClient` with `TailscaleWebProxy` created in `CreateHttpClient()` but never disposed
```csharp
private static HttpClient CreateHttpClient() {
    var handler = new HttpClientHandler { Proxy = new TailscaleWebProxy(), UseProxy = true };
    var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    return client; // Never disposed!
}
```
**Why it matters:** Singleton `HttpClient` holds connections; `TailscaleWebProxy` references `AppState.Tailscale` creating circular reference. On TV app resume/suspend cycles, sockets accumulate.
**Fix:** Implement `IDisposable` on `JellyfinService`, dispose `_http` in `AppState.ClearSession()`.

---

### 5. [CRITICAL] File: `Screens/VideoPlayerScreen.cs` | Lines: 69-972+
**Issue:** Massive memory leaks - multiple `Timer` (Tizen.NUI.Timer) created but not all stopped/disposed
```csharp
_reportProgressTimer = new Timer(5000);
_reportProgressTimer.Tick += OnReportProgressTick;
_reportProgressTimer.Start();
// Only stopped in OnHide, but what if screen popped without OnHide?
```
**Why it matters:** 15+ Timer instances, Animation objects, HttpClient for trickplay, subtitle parsers - all can leak on rapid navigation.
**Fix:** Implement `IDisposable` on `VideoPlayerScreen`, override `OnHide` to dispose ALL timers/animations/clients. Use `ScreenBase.DisposeTimer()` pattern consistently.

---

### 6. [CRITICAL] File: `Core/TailscaleDebugLog.cs` | Lines: 14, 34
**Issue:** Static event `LogAdded` never unsubscribed by screens
```csharp
public static event Action LogAdded;
```
**Why it matters:** Every screen that calls `CreateDebugOverlay()` subscribes: `TailscaleDebugLog.LogAdded += OnLogAdded;` (ScreenBase:67). When screens are popped, `HideDebugOverlay()` unsubscribes, but if screen is disposed without `OnHide()`, handler leaks → memory grows per navigation.
**Fix:** Make `LogAdded` weak event or ensure `OnHide()` always called (use `try/finally` in NavigationService).

---

### 7. [CRITICAL] File: `Screens/HomeLoadingScreen.cs` | Lines: 26-358
**Issue:** Static `ConcurrentDictionary` cache never cleared, grows unbounded
```csharp
private static readonly ConcurrentDictionary<string, string> ImageUrlCache = new();
private const int ImageUrlCacheMaxEntries = 2500;
```
**Why it matters:** 2500 entries × ~200 chars = ~500KB minimum, but keys never expire. On long sessions with many libraries, memory grows.
**Fix:** Add TTL expiration or LRU eviction with timestamp.

---

### 8. [CRITICAL] File: `Core/AppState.cs` | Lines: 902-989
**Issue:** `OnAppResumed()` has race conditions and blocking calls
```csharp
NavigationService.ShowReconnectOverlay("...");
System.Threading.Tasks.Task.Run(async () => {
    int attempts = 0;
    while (attempts < 15) {
        if (IsTailscaleConnected()) { ... }
        await Task.Delay(1000);
        attempts++;
    }
});
```
**Why it matters:** 
- `IsTailscaleConnected()` calls `.GetAwaiter().GetResult()` (blocking) inside background task
- Multiple concurrent resume handlers possible if app suspended/resumed rapidly
- `ShowReconnectOverlay`/`HideReconnectOverlay` not thread-safe
**Fix:** Add `_resumeInProgress` lock, make `IsTailscaleConnected()` async, use `CoreApplication.Post` for all UI updates.

---

## MODERATE Issues (Degraded Experience)

### 9. [MODERATE] File: `Core/TailscaleService.cs` | Lines: 288-328
**Issue:** `WatchIPNBus` creates `HttpClient` per call, never disposed
```csharp
using var client = CreateLocalApiClient(); // Creates new HttpClient each call
```
**Fix:** Reuse single `HttpClient` per `TailscaleService` instance.

---

### 10. [MODERATE] File: `Screens/StartupScreen.cs` | Lines: 60-93
**Issue:** Tailscale detection logic duplicated in `StartupScreen` and `ServerPickerScreen`
```csharp
var active = AppState.GetStoredServers().FirstOrDefault(s => s.IsActive);
bool isTailscaleServer = host.StartsWith("100.") || ... // Duplicated in 3+ files
```
**Fix:** Extract to `AppState.IsTailscaleUrl()` (already exists at line 842) and use consistently.

---

### 11. [MODERATE] File: `Screens/VideoPlayerScreen.cs` | Lines: 340-768
**Issue:** `StartPlaybackAsync()` is 400+ lines, does too much (auth, media selection, subtitle handling, player setup)
**Why it matters:** Hard to test, debug, maintain. Subtitle logic alone is 200+ lines.
**Fix:** Extract `ResolveMediaSourceAsync`, `ConfigureSubtitlesAsync`, `BuildStreamUrl` methods.

---

### 12. [MODERATE] File: `Screens/MovieDetailsScreen.cs` & `EpisodeDetailsScreen.cs`
**Issue:** ~80% duplicate code between these two screens (both ~1300 lines)
**Fix:** Create `DetailsScreenBase` abstract class with shared logic.

---

### 13. [MODERATE] File: `UI/MediaCardFactory.cs` | Lines: 193-304
**Issue:** Complex text measurement logic with magic numbers, no unit tests
```csharp
float approximateCharWidth = Math.Max(6f, pointSize * 0.54f);
```
**Why it matters:** Text wrapping bugs on different TV resolutions/DPI.
**Fix:** Use NUI's built-in text measurement or add configurable constants.

---

### 14. [MODERATE] File: `Core/AppState.cs` | Lines: 1057-1070
**Issue:** `HttpClient.Timeout = 10 seconds` hardcoded
```csharp
Timeout = TimeSpan.FromSeconds(10)
```
**Why it matters:** Too short for Tailscale relay connections on slow networks; no configuration.
**Fix:** Make configurable via `AppState` or settings screen.

---

### 15. [MODERATE] File: `Screens/ServerSetupScreen.cs` | Lines: 249-321
**Issue:** `ResolveServerBaseUrl` creates new `HttpClient` per probe, doesn't reuse connection
```csharp
using var httpClient = new System.Net.Http.HttpClient(handler);
```
**Fix:** Reuse single `HttpClient` with connection pooling.

---

## MINOR Issues (Cleanup, Simplification, Style)

### 16. [MINOR] File: `Core/NavigationService.cs` | Lines: 26-41
**Issue:** `KeyDebugLogs` list grows unbounded (max 40 entries but no cleanup on app suspend)
```csharp
public static readonly List<string> KeyDebugLogs = new();
```
**Fix:** Clear on `NotifyAppTerminating()` or make circular buffer.

---

### 17. [MINOR] File: `Screens/ScreenBase.cs` | Lines: 137-147
**Issue:** `FireAndForget` swallows exceptions silently
```csharp
task.ContinueWith(faultedTask => { _ = faultedTask.Exception; }, ...);
```
**Fix:** Log exceptions: `TailscaleDebugLog.Add($"FireAndForget error: {faultedTask.Exception}");`

---

### 18. [MINOR] File: `Utils/CacheHelper.cs` | Lines: 28-43
**Issue:** No cache size limit, no background cleanup
```csharp
private static readonly ConcurrentDictionary<string, CacheItem> _cache = new();
```
**Fix:** Add `MaxEntries` with LRU eviction.

---

### 19. [MINOR] File: `UI/MonochromeAuthFactory.cs` | Lines: 106-213
**Issue:** `CreateInputFieldShell` creates `Timer` for cursor blink but never disposes it
```csharp
var placeholderCursorBlinkTimer = new Timer(AuthPlaceholderCursorBlinkIntervalMs);
placeholderCursorBlinkTimer.Tick += ...;
```
**Fix:** Return `IDisposable` wrapper or attach to parent view lifecycle.

---

### 20. [MINOR] File: `Core/TailscaleService.cs` | Lines: 440-490
**Issue:** `GetFreePort`/`GetFreeUdpPort` race condition - port checked then used later
```csharp
var listener = new TcpListener(IPAddress.Loopback, defaultPort);
listener.Start();
listener.Stop();
return defaultPort; // Port could be taken by another process NOW
```
**Fix:** Bind and hold the socket until actually used, or accept race as low-probability.

---

### 21. [MINOR] File: `Screens/VideoPlayerScreen.cs` | Lines: 789-802
**Issue:** `RewriteStreamUrlForTailscale` duplicates logic from `AppState.RewriteImageUrlForTailscale`
**Fix:** Centralize in `AppState` or `TailscaleProxyService`.

---

### 22. [MINOR] File: Multiple files
**Issue:** `async void` used in event handlers correctly, but `FireAndForget` pattern creates untracked tasks
**Fix:** Use `Task.Run` with proper exception handling or `IAsyncCommand` pattern.

---

### 23. [MINOR] File: `Utils/PerfTrace.cs`
**Issue:** `PerfTrace.Enabled = false` by default, but `DebugSwitches` has separate flags
**Fix:** Unify debug flags or use `PerfTrace` consistently.

---

### 24. [MINOR] File: `UI/AppleTvLoadingVisual.cs`
**Issue:** Animations not stopped if `Stop()` called during animation
**Fix:** Add null checks and `try/catch` in `Stop()`.

---

### 25. [MINOR] File: `Models/Capabilities.cs`
**Issue:** DTO classes with no validation, used for JSON serialization directly
**Fix:** Add `JsonConverter` for enum-like fields or validation.

---

## STABILITY-SPECIFIC CHECKS (Smart TV Context)

### 26. [STABILITY] Startup Sequence - `App.cs` → `AppState.Init()` → `StartupScreen`
**Issues:**
- `AppState.Init()` starts Tailscale in background but `StartupScreen` waits with `Task.WhenAny(AppState.TailscaleReadyTask, Task.Delay(10000))` (line 78)
- If Tailscale binary missing, app shows error but continues - no fallback to non-Tailscale mode clearly communicated
- 12-second fallback timer in `StartupScreen` (line 96) may fire before Tailscale ready

**Recommendation:** Add explicit startup states: `Initializing → TailscaleStarting → TailscaleReady/Failed → ServerConnecting → Ready`

---

### 27. [STABILITY] Tailscale Connection Drops
**Issues:**
- `TailscaleScreen.PeriodicRefreshAsync` polls every 2s but only updates UI on state change
- No automatic reconnection attempt when `BackendState` becomes `Stopped` or `NeedsLogin`
- `AppState.OnAppResumed()` attempts restart but no exponential backoff

**Recommendation:** Implement `TailscaleConnectionMonitor` service with:
- Exponential backoff reconnection (1s, 2s, 4s, 8s, max 60s)
- Network change detection (Tizen connectivity events)
- User notification only after 3 failed attempts

---

### 28. [STABILITY] Jellyfin Server Communication
**Issues:**
- `JellyfinService.GetAsync()` throws `HttpRequestException` on non-success - callers must catch
- No retry logic for transient failures (5xx, timeout)
- `TimeoutException` from `WithTimeout` not distinguished from server errors

**Recommendation:** Add `Polly` retry policy or custom `RetryAsync` with:
- 3 retries with exponential backoff for 5xx/timeout
- Circuit breaker after 5 consecutive failures
- Separate timeout for image vs API calls

---

### 29. [STABILITY] Hardcoded Values
| File | Line | Value | Should Be |
|------|------|-------|-----------|
| `AppState.cs` | 1066 | `Timeout = TimeSpan.FromSeconds(10)` | Configurable |
| `VideoPlayerScreen.cs` | 217 | `PlayerBufferInitialMs = 6000` | Per-device tuning |
| `VideoPlayerScreen.cs` | 218 | `PlayerBufferResumeMs = 4000` | Per-device tuning |
| `TailscaleService.cs` | 298 | `attempts < 30` (30s socket wait) | Configurable |
| `StartupScreen.cs` | 96 | `12000` fallback timer | Based on Tailscale startup |
| `HomeLoadingScreen.cs` | 66 | `25000` fallback timer | Configurable |

---

### 30. [STABILITY] Resource Cleanup on App Terminate
**File:** `App.cs` lines 35-45
```csharp
protected override void OnTerminate() {
    try { AppState.TailscaleProxy?.Stop(); AppState.Tailscale?.Stop(); } catch { }
    NavigationService.NotifyAppTerminating();
    base.OnTerminate();
}
```
**Issues:**
- `HttpClient` in `JellyfinService` not disposed
- `TailscaleProxyService._forwardClient` not disposed
- Static caches (`TailscaleDebugLog`, `CacheHelper`, `HomeLoadingScreen.ImageUrlCache`) not cleared
- `TailscaleService` process kill may leave orphaned `tailscaled`

**Fix:** Implement `AppState.Shutdown()` that disposes all services, call from `OnTerminate()`.

---

## PRIORITIZED ACTION LIST (Top 5 for Biggest Stability Impact)

| Priority | Issue | File(s) | Effort | Impact |
|----------|-------|---------|--------|--------|
| **1** | Implement proper `IDisposable` pattern across all services (`JellyfinService`, `TailscaleService`, `TailscaleProxyService`, `VideoPlayerScreen`) | `Core/*.cs`, `Screens/VideoPlayerScreen.cs` | Medium | **High** - Prevents socket/handle leaks on long TV sessions |
| **2** | Fix `TailscaleService.StageAndStart()` blocking `.Wait()` and make async throughout | `Core/TailscaleService.cs`, `Core/AppState.cs`, `Screens/StartupScreen.cs` | Medium | **High** - Eliminates startup deadlock/ANR risk |
| **3** | Add retry logic + circuit breaker to `JellyfinService` network calls | `Core/JellyfinService.cs` | Medium | **High** - Handles transient network/Tailscale issues gracefully |
| **4** | Centralize Tailscale connection monitoring with exponential backoff | New `Core/TailscaleConnectionMonitor.cs` + `AppState.OnAppResumed()` | Medium | **High** - Auto-recovery from VPN drops without user action |
| **5** | Extract `DetailsScreenBase` to eliminate Movie/Episode details duplication | `Screens/MovieDetailsScreen.cs`, `EpisodeDetailsScreen.cs` | Low | **Medium** - Reduces bug surface by ~1500 lines |

---

## Additional Recommendations

1. **Add integration tests** for Tailscale startup/auth flow using emulator
2. **Enable `PerfTrace`** in debug builds to catch UI thread blocking
3. **Add memory profiling** to CI (monitor `GC.GetTotalMemory()` during navigation stress test)
4. **Document Tizen-specific constraints** in `AGENTS.md` (e.g., no `tun` device, userspace networking only)
5. **Consider upgrading to .NET 8** when Tizen.NET supports it (LTS, better performance)

---

*Generated on: Mon Jul 06 2026*
*Repository: jellyfin-dotnet (Jellyfin Tizen Client with Tailscale Integration)*