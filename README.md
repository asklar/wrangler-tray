# Wrangler Tray

A Windows system tray app that monitors your Cloudflare Workers and Pages deployments and notifies you when they complete or fail.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
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
- .NET 8.0 Runtime
- (Optional) Node.js + wrangler CLI for browser-based login

## Getting Started

1. **Build and run:**
   ```bash
   cd WranglerTray
   dotnet run
   ```

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

## License

MIT
