# BetterLootTracker

OriathHub plugin for Path of Exile 2 that tracks currency loot per map and session, with host pricing and an in-game HUD overlay.

**Author:** Raff  
**Version:** 1.9.4

## Features

- Per-map and session loot totals with divine / chaos / exalted display
- Best map, last map, and current map tracking
- HUD overlay with sortable currency table (Item | Qty | Price)
- Configurable map value threshold (green / red rating)
- Host pricing via OriathHub `Core.Prices`
- Session history saved on reset

## Requirements

- [OriathHub](https://github.com/danthespal/OriathHubSDK) with SDK 0.10.1+
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (build from source only)

## Install

**OriathHub Marketplace (recommended):** install or update from the in-app catalog. Marketplace builds from this repo’s source, or installs the latest [Release zip](https://github.com/RAcbd/BetterLootTracker/releases).

**Manual from Release:** download `BetterLootTracker-<version>.zip` from [Releases](https://github.com/RAcbd/BetterLootTracker/releases) and extract into your OriathHub `Plugins/` folder.

**Manual from source:** clone this repo and build (see below), then copy the output DLLs plus `config/` and `data/` into `Plugins/BetterLootTracker/`.

## Repository layout

This repo is **source only**. DLLs and release zips are not committed — they are published on [GitHub Releases](https://github.com/RAcbd/BetterLootTracker/releases) when tagged.

```
BetterLootTracker/
  src/BetterLootTracker/   # C# source
  config/                  # Example settings
  data/                    # Default currency name mappings
```

## Build from source

```powershell
cd src/BetterLootTracker
dotnet restore
dotnet build -c Release
```

## Configuration

| File | Purpose |
|------|---------|
| `config/settings.json` | Plugin settings (created on first run; see `config/settings.json.example`) |
| `data/currency-names.json` | Display name overrides and manual price ID links (`byPath`, `byPathNinjaId`) |

Most currencies are priced automatically. If an item shows `—`, add a `byPathNinjaId` entry with the poe.ninja item id, then click **Reload currency names** in the dashboard.

## License

MIT — see [LICENSE](LICENSE).
