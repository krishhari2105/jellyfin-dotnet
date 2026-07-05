# Jellyfin Tizen C#/.NET Client - AI Agent Guidelines

This file serves as a guide for AI agents (and human developers) working on the **Jellyfin Tizen Client with Tailscale Integration** codebase. It outlines the project architecture, tech stack, UI guidelines, keyboard/remote navigation conventions, Tailscale integration details, and performance/Tizen-specific gotchas.

---

## 1. Repository Overview & Stack

- **Target Framework**: .NET 6.0 (`net6.0-tizen9.0`) targeting Samsung Smart TVs (Tizen API version 9.0).
- **UI Toolkit**: **Tizen NUI (Natural User Interface)**, a high-performance C# UI framework wrapping Samsung's native DALi graphics engine.
- **Core Dependencies**:
  - `Tizen.NET`: Tizen system APIs, application lifecycle, and NUI controls.
  - `QRCoder`: Used for generating QR codes for Tailscale authentication.
- **Execution Model**:
  - `jellyfin` is the primary executable project.
  - Build scripts for cross-compiling Tailscale static binaries reside in `tailscale-tizen/` (if present).
  - Executable/native binaries for Tailscale are bundled into the final TPK under `lib/`.

---

## 2. Directory & Component Structure

- [App.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/App.cs): The entry point of the NUI application. Inherits from `NUIApplication`. Handles application lifecycle callbacks (`OnCreate`, `OnPause`, `OnResume`, `OnTerminate`).
- **Core/**: Core system logic and services.
  - [AppState.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/AppState.cs): Manages server connections, auth tokens, device credentials, saved servers registry, and orchestrates the startup/lifecycle of the Tailscale daemon and proxy.
  - [NavigationService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/NavigationService.cs): Custom stack-based navigation manager. Grabs hardware remote keys (using EFL P/Invokes) and forwards them to active screen handlers.
  - [JellyfinService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/JellyfinService.cs): Direct API service wrapper for Jellyfin. Implements authentication, library querying, and session tracking.
  - [TailscaleService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/TailscaleService.cs): Manages the subprocess lifecycle of `tailscaled`, extracts authentication URLs, and configures proxy ports.
  - [TailscaleProxyService.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Core/TailscaleProxyService.cs): An internal HTTP proxy that routes HTTP traffic to Jellyfin instances accessible only via the private Tailnet.
- **Screens/**: Full-screen UI views inheriting from `ScreenBase`.
  - [ScreenBase.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Screens/ScreenBase.cs): Abstract base class for all screens. Provides UI thread posting utilities (`RunOnUiThread`), fire-and-forget task continuation wrappers, timer helpers, and debugging overlays.
  - [VideoPlayerScreen.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Screens/VideoPlayerScreen.cs): The media playback engine. Handles subtitle tracks, trickplay, audio selection, playback state reporting, and rewrites streaming URLs to pass through the local proxy.
- **UI/**: Styling components, thematic tokens, and UI layout builders.
  - [UiTheme.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/UI/UiTheme.cs): Central design tokens (colors, font sizes, paddings, scales, borders) for the entire application.
  - [MonochromeAuthFactory.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/UI/MonochromeAuthFactory.cs): Layout builders for authentication, dialogs, buttons, fields, and panels.
- **Utils/**: General helper classes.
  - [QrCodeHelper.cs](file:///d:/Apps/github-repos/jellyfin-dotnet/jellyfin/Utils/QrCodeHelper.cs): Encodes URLs into visual QR codes.
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
3. **Back Key Convention**:
   - Pressing the remote's `Back` button must either pop the current screen from the navigation stack (`NavigationService.NavigateBack()`) or, if on the root screen, display the exit confirmation modal.
   - Never bypass `NavigationService` to close the app directly.

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

## 6. Tailscale & Proxying Details

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
