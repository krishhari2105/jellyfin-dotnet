# .NET Tizen to Tizen Web Porting Plan

This repository currently contains a .NET Tizen implementation. A complete rewrite to Tizen Web requires translating runtime APIs and UI architecture.

## Current to target architecture mapping

- `jellyfin/Core/JellyfinService.cs` -> `tizen-web/src/api/jellyfinApi.js`
- `jellyfin/Core/AppState.cs` -> `tizen-web/src/core/storage.js` + state module
- `jellyfin/Core/NavigationService.cs` -> route/state machine in JS
- `jellyfin/Screens/*.cs` -> `tizen-web/src/views/*`
- `jellyfin/UI/*.cs` -> reusable DOM components + CSS modules
- `jellyfin/Screens/VideoPlayerScreen*.cs` -> AVPlay wrapper + overlays

## Gap list still pending

- Remote keymap parity and focus engine parity
- Exact visual parity for every screen
- Playback telemetry, audio/subtitle switches, trickplay timeline
- Advanced auth/session flows and multi-user polishing
- Settings and server picker parity

## Delivery strategy

Port incrementally and verify each screen against the existing app before moving to the next one.
