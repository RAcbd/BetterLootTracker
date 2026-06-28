# BetterLootTracker

OriathHub plugin for Path of Exile 2 that tracks currency loot per map and session, with host pricing and an in-game HUD overlay.

**Author:** Raff  
**Version:** 1.9.4

## Install

**OriathHub Marketplace (recommended):** install or update from the in-app catalog — builds from this repo or installs the latest [Release zip](https://github.com/RAcbd/BetterLootTracker/releases).

**Manual from Release:** download `BetterLootTracker-<version>.zip` — contains a single `BetterLootTracker.dll` (shared code merged in).

## Repository layout

Source-only repo. Flat layout — no nested `src/BetterLootTracker/`, no committed `config/` or `data/` (created at runtime).

```
BetterLootTracker/
  *.cs
  BetterLootTracker.csproj
  SDK/                 # OriathHub.Sdk.nupkg (offline / Marketplace builds)
  OriathPlugins.Common/
  build/
```

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download). The [OriathHub SDK](https://github.com/danthespal/OriathHubSDK) package is bundled in `SDK/` — no external setup needed to build.

## Build

```powershell
dotnet build BetterLootTracker.csproj -c Release
```

Release output is a **single** `bin/Release/net10.0-windows/BetterLootTracker.dll`.

## License

MIT — see [LICENSE](LICENSE).
