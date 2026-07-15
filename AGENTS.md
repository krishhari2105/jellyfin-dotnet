# Jellyfin Tizen C#/.NET Client - AI Agent Guidelines

This file serves as a guide for AI agents (and human developers) working on the **Jellyfin Tizen Client with Tailscale Integration** codebase. It outlines the project architecture, tech stack, UI guidelines, keyboard/remote navigation conventions, Tailscale integration details, and performance/Tizen-specific gotchas.

---

## 1. Repository Overview & Stack

- **Target Framework**: .NET 6.0 (`net6.0-tizen9.0`) targeting Samsung Smart TVs (Tizen API version 9.0).
- **UI Toolkit**: **Tizen NUI (Natural User Interface)**, a high-performance C# UI framework wrapping Samsung's native DALi graphics engine.
- **Build Configurations**:
  The project supports two build configurations (flavors) using MSBuild properties:
  1. **Tailscale-enabled (Default)**: Compiles all VPN support, includes the QRCoder package, and bundles the `tailscaled` binary.
  2. **Standalone (Without Tailscale)**: Compiles out all Tailscale-specific code, screens, and dependencies. Output is built with the `-p:TailscaleEnabled=false` option.
- **Core Dependencies**:
  - `Tizen.NET`: Tizen system APIs, application lifecycle, and NUI controls.
  - `QRCoder`: Used for generating QR codes for Tailscale authentication (included *only* in Tailscale-enabled flavor).
- **Execution Model**:
  - `jellyfin` is the primary executable project.
  - Build scripts for cross-compiling Tailscale static binaries reside in `tailscale-tizen/` (if present).
  - Executable/native binaries for Tailscale are bundled into the final TPK under `lib/` (excluded in standalone flavor).

---

## 2. Directory & Component Structure

- [App.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/App.cs): The entry point of the NUI application. Inherits from `NUIApplication`. Handles application lifecycle callbacks (`OnCreate`, `OnPause`, `OnResume`, `OnTerminate`).
- **Core/**: Core system logic and services.
  - [AppState.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/AppState.cs): Manages server connections, auth tokens, device credentials, saved servers registry. Orchestrates Tailscale daemon/proxy startup if compiled with Tailscale support.
  - [NavigationService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/NavigationService.cs): Custom stack-based navigation manager. Grabs hardware remote keys (using EFL P/Invokes) and forwards them to active screen handlers. Manages page transition animations.
  - [JellyfinService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/JellyfinService.cs): Direct API service wrapper for Jellyfin. Implements authentication, library querying, and session tracking.
  - [TailscaleService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/TailscaleService.cs) *(Tailscale-only)*: Manages the subprocess lifecycle of `tailscaled`, extracts authentication URLs, and configures proxy ports.
  - [TailscaleProxyService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/TailscaleProxyService.cs) *(Tailscale-only)*: An internal HTTP proxy that routes HTTP traffic to Jellyfin instances accessible only via the private Tailnet.
  - [TailscaleWebProxy.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/TailscaleWebProxy.cs) *(Tailscale-only)*: Custom `IWebProxy` implementation for routing local SOCKS5 proxy calls.
  - [TailscaleConnectionMonitor.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/TailscaleConnectionMonitor.cs) *(Tailscale-only)*: Monitors VPN connectivity.
- **Screens/**: Full-screen UI views inheriting from `ScreenBase`.
  - [ScreenBase.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Screens/ScreenBase.cs): Abstract base class for all screens. Provides UI thread posting utilities (`RunOnUiThread`), fire-and-forget task continuation wrappers, timer helpers, and debugging overlays.
  - [VideoPlayerScreen.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Screens/VideoPlayerScreen.cs): The media playback engine. Handles subtitle tracks, trickplay, audio selection, playback state reporting, and rewrites streaming URLs to pass through the local proxy (if Tailscale is enabled).
  - [TailscaleAuthScreen.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Screens/TailscaleAuthScreen.cs) *(Tailscale-only)*: QR code authentication setup screen for VPN connection.
  - [TailscaleScreen.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Screens/TailscaleScreen.cs) *(Tailscale-only)*: Screen showcasing Tailscale VPN connection status and info.
- **UI/**: Styling components, thematic tokens, and UI layout builders.
  - [UiTheme.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/UI/UiTheme.cs): Central design tokens (colors, font sizes, paddings, scales, borders) for the entire application.
  - [MonochromeAuthFactory.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/UI/MonochromeAuthFactory.cs): Layout builders for authentication, dialogs, buttons, fields, and panels.
- **Utils/**: General helper classes.
  - [QrCodeHelper.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Utils/QrCodeHelper.cs) *(Tailscale-only)*: Encodes URLs into visual QR codes using `QRCoder`.
  - [UiAnimator.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Utils/UiAnimator.cs): UI transition effects and focus scaling animations.

---

## 3. UI Styling & Theme Rules

1. **Design System Consistency**:
   - Never use raw/hardcoded colors or layout specs in screen UI.
   - Always reference variables inside [UiTheme.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/UI/UiTheme.cs).
   - Standard palette colors: `UiTheme.Background`, `UiTheme.Surface`, `UiTheme.SurfaceFocused`, `UiTheme.Accent`, `UiTheme.TextPrimary`, etc.
2. **NUI Views**:
   - Layout panels using [MonochromeAuthFactory.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/UI/MonochromeAuthFactory.cs) for consistent corners, borders, and margins.
   - Keep in mind that Tizen NUI handles coordinates imperatively. Set `WidthResizePolicy = ResizePolicyType.FillToParent` or `ResizePolicyType.FitToChildren` appropriately.
   - For text elements, use `TextLabel`. Remember to specify `LineWrapMode = LineWrapMode.Word` and `MultiLine = true` if text might overflow.
3. **Focus Styling**:
   - Default blue focus indicators are disabled globally in `NavigationService.Init()` (`FocusManager.Instance.FocusIndicator = null;`).
   - You must implement visual focus feedback manually on interactive views. Use `FocusGained` and `FocusLost` events to transition colors (e.g., swapping `Surface` with `SurfaceFocused`) or scale views using `UiAnimator`.

---

## 4. Keyboard & Remote Control Model

TV clients are navigated exclusively via a remote control D-pad.

1. **Key Handlers**:
   - Screens that handle user inputs must implement [IKeyHandler](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/IKeyHandler.cs) and override `HandleKey(AppKey key)`.
   - Mapped keys [AppKey](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/AppKey.cs) include: `Up`, `Down`, `Left`, `Right`, `Enter`, `Back`, and Media keys like `MediaPlay`, `MediaPause`, `MediaPlayPause`.
2. **Native Key Grab Interop**:
   - Standard navigation keys flow through normal window key events.
   - Media keys (Play, Pause, Stop, etc.) and color hotkeys (like the Red key) require EFL-specific key grabbing using P/Invoke signatures in `NavigationService`:
     ```csharp
     [DllImport("libefl-extension.so.0", EntryPoint = "eext_win_keygrab_set")]
     private static extern bool eext_win_keygrab_set(IntPtr window, string key);
     ```
   - Make sure you do not intercept keys globally that would break basic platform capabilities, and handle key grabbing conditionally.
3. **Back Key & Transitions**:
   - Pressing the remote's `Back` button must either pop the current screen from the navigation stack (`NavigationService.NavigateBack()`) or, if on the root screen, display the exit confirmation modal.
   - Never bypass `NavigationService` to close the app directly.
   - Backward navigation features a lightweight fade-from-black transition (`PlayBackNavigationFade`). This uses a solid-black topmost view overlay rather than animating the screen subtree's opacity to prevent stuttering/dropped frames on Tizen TV hardware.

---

## 5. Concurrency & Lifecycles

1. **UI Thread Safety**:
   - Any modifications to NUI visual trees, properties, or styles MUST be executed on the UI main thread.
   - Use `RunOnUiThread(Action)` inside screens (which forwards to `CoreApplication.Post`) to dispatch UI updates from background threads.
2. **Resource Disposal & Memory Leaks**:
   - Long-running TV applications are prone to memory exhaustion.
   - Always clean up timers, unsubscribe from event handlers (especially static events), and cancel pending HTTP/TCP operations in the screen's `OnHide()` or `Dispose()` methods.
   - Use the `DisposeTimer(ref ThreadingTimer)` helper in `ScreenBase` to safely release timing tasks.

---

## 6. Tailscale & Proxying Details *(Tailscale Flavor Only)*

1. **Daemon Management**:
   - `TailscaleService` handles binary packaging and subprocess staging.
   - If running inside the Tizen Emulator (x86 architecture), it targets `lib/tailscaled-x86`.
   - If running on a real Samsung TV (ARM architecture), it targets `lib/tailscaled`.
   - Runs in userspace-networking mode (`--tun=userspace-networking`) because TV applications do not have root privileges to create virtual network interfaces (`tun` devices).
2. **Proxy Redirection**:
   - All network traffic bound for the Jellyfin server must be rewritten and resolved through the local loopback proxy (`127.0.0.1:3128` or custom SOCKS5/HTTP port mappings setup by the daemon).
   - `TailscaleProxyService` manages local request routing. Image URLs and video streaming links are intercepted and rewrote to route via proxy when server URLs match Tailnet domains or IPs.

---

## 7. Performance & Gotchas

1. **Garbage Collection Optimization**:
   - Avoid creating temporary objects, event delegate allocations, or layout triggers during list scrolls (e.g., movie catalogs) or media playback ticks.
2. **Image Loading Race Conditions**:
   - When launching the Tailscale proxy, wait at least **500ms** before performing requests or image loads. Failure to do so can result in failed image fetching and black UI rendering due to the local listener port not binding fast enough.
3. **Tizen Manifest**:
   - Privileges for internet connectivity (`http://tizen.org/privilege/internet`) and TV input keys (`http://tizen.org/privilege/tv.inputdevice`) must be declared in [tizen-manifest.xml](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/tizen-manifest.xml).

---

## 8. Build Flavors & Conditional Compilation

### Build Flavors & Redirection

The repository defines two distinct flavors in [Directory.Build.props](file:///d:/Apps/github-repos/jellyfin-dotnet/Directory.Build.props) and [jellyfin.csproj](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/jellyfin.csproj). Output pathways are isolated to prevent compiler cache conflicts:
- **Tailscale Build**: Outputs to `bin/` and `obj/`.
- **Standalone Build**: Outputs to `bin-standalone/` and `obj-standalone/`.

Both build flavors utilize the MSBuild `<DefaultItemExcludes>` property to prevent each configuration from recursively globbing intermediate files generated by the other.

### MSBuild Configuration Rules
```xml
<!-- Default behavior when TailscaleEnabled is not explicitly passed -->
<TailscaleEnabled Condition=" '$(TailscaleEnabled)' == '' ">true</TailscaleEnabled>

<!-- Define TAILSCALE compiler constant if enabled -->
<PropertyGroup Condition=" '$(TailscaleEnabled)' == 'true' ">
  <DefineConstants>$(DefineConstants);TAILSCALE</DefineConstants>
</PropertyGroup>
```

When building with `TailscaleEnabled` set to `false` (e.g. `dotnet build -p:TailscaleEnabled=false`), the following changes are applied:
1. **Compilation Removal**: Excludes Tailscale-specific source files (`Core/Tailscale*.cs`, `Screens/Tailscale*.cs`, `Utils/QrCodeHelper.cs`) using `<Compile Remove="..." />`.
2. **Dependency Omission**: The `QRCoder` package reference is excluded using `Condition=" '$(TailscaleEnabled)' == 'true' "`.
3. **Binary Packaging Exclusion**: Bundled static binaries (`tailscaled` and `tailscaled-x86` under `lib/`) are removed from TPK generation via `<TizenTpkUserExcludeFiles Include="..." />` rules.

### Conditional C# Code Guidelines
Any logic that references Tailscale services, screens, or features in shared files (e.g. `AppState.cs`, `NavigationService.cs`, `VideoPlayerScreen.cs`, `StartupScreen.cs`) MUST be wrapped in `#if TAILSCALE ... #endif` block directives.
```csharp
#if TAILSCALE
    // Tailscale-specific logic, service calls, or navigation to Tailscale screens
#endif
```
