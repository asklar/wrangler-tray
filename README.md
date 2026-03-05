# Wrangler Tray

A Windows system tray app that monitors your Cloudflare Workers and Pages deployments and notifies you when they complete or fail.

![.NET 10](https://img.shields.io/badge/.NET-10.0-blue)
![WPF](https://img.shields.io/badge/UI-WPF-purple)
![Cloudflare](https://img.shields.io/badge/Cloudflare-API-orange)

## Features

- **System tray app** — runs quietly in the background, click the tray icon to see deployments
- **Monitors both Workers and Pages** deployments across all your Cloudflare accounts
- **Windows toast notifications** when deployments succeed, fail, or are newly created
- **Dual authentication:**
  - `wrangler login` — browser-based OAuth (preferred, reuses Cloudflare's own flow)
  - API Token — paste a token from the Cloudflare dashboard (no wrangler required)
- **Auto-detects wrangler** and offers to install it via npm if missing
- **Configurable** polling interval, notification preferences, startup with Windows

## Requirements

- Windows 10 or later
- (Optional) Node.js + wrangler CLI for browser-based login

## Installation

1. Download the latest `WranglerTray-vX.X.X-win-x64.zip` from [Releases](https://github.com/asklar/wrangler-tray/releases)
2. Extract the zip to a folder (e.g. `C:\Program Files\WranglerTray`)
3. Run `WranglerTray.exe`

The release is a self-contained single-file binary — no .NET runtime installation required.

## Getting Started

1. **Run `WranglerTray.exe`** — the app starts in the system tray

2. **Right-click the tray icon** → Settings

3. **Log in** using either:
   - Click "Login with Wrangler" to open your browser and authenticate with Cloudflare
   - Or paste a Cloudflare API token with read permissions for Workers and Pages

4. **Left-click the tray icon** to see your recent deployments

## API Token Permissions

If using an API token, create one at [dash.cloudflare.com/profile/api-tokens](https://dash.cloudflare.com/profile/api-tokens) with:
- `Account` → `Workers Scripts` → `Read`
- `Account` → `Cloudflare Pages` → `Read`

## Architecture

```
WranglerTray/
├── Models/           — Data models and API DTOs
├── Services/         — Auth, API client, polling monitor, notifications, settings
├── ViewModels/       — MVVM view models (CommunityToolkit.Mvvm)
├── Views/            — WPF windows (deployment list, settings)
└── App.xaml.cs       — Entry point, tray icon setup, service wiring
```

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
cd WranglerTray
dotnet run                # run in debug mode
dotnet publish -c Release -r win-x64 /p:PublishAot=true  # native AOT build
```

## Releasing

Push a version tag to trigger the GitHub Actions release pipeline:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This builds a native AOT binary, zips it, and publishes it as a GitHub Release.

## License

MIT
