# Jellyfin Tizen Web Port Starter

This folder is a **starter migration project** to move the existing .NET Tizen app to a Tizen Web app (JavaScript/HTML/CSS) without starting from a blank project.

## What is already included

- Minimal Tizen-web-compatible app shell (`index.html` + ES modules).
- Jellyfin API client covering:
  - server availability check
  - public user listing
  - username/password authentication
  - resume feed
  - latest library feed
- State persistence via `localStorage`.
- First-pass screens:
  - server setup
  - login
  - home with continue-watching and latest items
- TV-friendly CSS focus styling.

## How to run locally

Because this starter is plain static assets, you can run it with any local static server:

```bash
python3 -m http.server 8080 -d tizen-web
```

Then open `http://localhost:8080`.

## Recommended migration order

1. **Core services parity**: move `Core/JellyfinService.cs`, auth/session handling, and navigation logic to JS modules.
2. **Screen-by-screen port**:
   - Startup / server setup
   - User selection & password
   - Home
   - Details screens (movie, series, season, episode)
   - Video player and overlays
   - Settings
3. **Input system parity**: map `IKeyHandler` patterns to Tizen remote key events (`tizen.tvinputdevice`).
4. **UI parity**: port reusable components from `UI/*` to DOM components.
5. **Playback parity**: integrate Tizen AVPlay APIs for full player feature compatibility.
6. **Manifest/build parity**: add `config.xml`, packaging scripts, and CI.

## Important note

A full one-shot port of the entire app is large and should be done in phases to avoid regressions. This starter accelerates that process and gives you a concrete baseline to continue from.
