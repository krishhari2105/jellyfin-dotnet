# Jellyfin Tizen C#/.NET Client - Agent Guidelines

## Repository and stack

- Target `net6.0-tizen9.0` with Tizen API 9.0.
- The application UI uses Tizen NUI and Samsung's DALi rendering engine.
- `jellyfin` is the single executable application project.
- `jellyfin.CacheHelper.Tests` links framework-neutral production helpers into an xUnit test host.
- Build the canonical application with `dotnet build jellyfin/jellyfin.csproj -c <Debug|Release>`.

## Architecture

- `jellyfin/App.cs`: application lifecycle entry point.
- `jellyfin/Core/AppState.cs`: shared application state, saved-server registry, session persistence, and service ownership.
- `jellyfin/Core/JellyfinService.cs`: Jellyfin HTTP API wrapper, retry policy, circuit breaker, playback information, and session reporting.
- `jellyfin/Core/NavigationService.cs`: screen stack, remote key routing, transitions, loading overlays, and exit confirmation.
- `jellyfin/Screens/ScreenBase.cs`: UI-thread dispatch, fault observation, timer/cancellation cleanup, and diagnostic overlay helpers.
- `jellyfin/Screens/VideoPlayerScreen.cs`: native playback, subtitles, trickplay, stream selection, and playback reporting.
- `jellyfin/UI/UiTheme.cs`: centralized design tokens.
- `jellyfin/UI/MonochromeAuthFactory.cs`: consistent authentication and server-management controls.

The normal connection flow is:

```text
Application -> configured Jellyfin server URL -> Jellyfin HTTP/WebSocket APIs
```

## UI and styling

- Do not introduce raw colors or layout constants when an appropriate `UiTheme` token exists.
- Use the existing UI factories for panels, fields, buttons, and dialogs.
- Choose `FillToParent` and `FitToChildren` resize policies deliberately.
- Set `MultiLine = true` and word wrapping on text that can overflow.
- The global focus indicator is disabled. Every interactive view must provide manual focus feedback through colors, borders, or `UiAnimator`.

## Remote input and navigation

- Screens handling input implement `IKeyHandler.HandleKey(AppKey)`.
- Preserve D-pad, Enter, Back, and media-key behavior.
- Use `NavigationService.Navigate`, `NavigateBack`, and the root exit confirmation. Do not close the application directly from a screen.
- Media and color keys that require native grabs are managed centrally in `NavigationService`.
- After removing or adding a control, verify every focus index and directional path.

## Threading and lifecycle

- NUI tree and property mutations must occur on the UI thread.
- Use `ScreenBase.RunOnUiThread` or `CoreApplication.Post` for background continuations.
- Use `Task` instead of `async void` except for genuine lifecycle/event handlers.
- Observe fire-and-forget tasks through `ScreenBase.FireAndForget`.
- Dispose timers, animations, cancellation sources, streams, and event subscriptions in `OnHide` or `Dispose`.
- Reuse the shared `AppState.HttpClient`; do not allocate an HTTP client per request. A screen-owned probe client may be reused for that screen and disposed in `OnHide`.

## Persistence and compatibility

- Preference keys and saved-server JSON are upgrade-sensitive.
- Additive JSON changes should tolerate unknown fields from newer or older application versions.
- Do not clear saved servers or authentication data during routine migrations unless explicitly required.
- Never log access tokens, authorization headers, or API keys. Use `SensitiveDataRedactor` through `AppDebugLog`.

## Performance

- Avoid allocations, layout churn, and new event delegates in scroll and playback hot paths.
- Keep image caches bounded and unload offscreen poster resources.
- Preserve the persistent loading spinner and lightweight black-overlay back-navigation fade.
- Cancel stale async screen generations before applying UI results.

## Tizen packaging

- Keep the existing package ID, application ID, label, icon, signing configuration, and API level unless a task explicitly changes them.
- The internet privilege is required for Jellyfin access.
- The TV input-device privilege is required for remote keys.
- Do not commit `.tpk` files, signing certificates, secrets, generated `bin`/`obj` content, or local IDE state.

## Validation

Use repository-derived commands:

```powershell
dotnet restore jellyfin.sln
dotnet build jellyfin/jellyfin.csproj -c Debug
dotnet build jellyfin/jellyfin.csproj -c Release
dotnet test jellyfin.CacheHelper.Tests/jellyfin.CacheHelper.Tests.csproj
git diff --check
```

When Tizen tooling and signing are available, verify the generated `.tpk` contents and install it on an emulator or Samsung TV.
