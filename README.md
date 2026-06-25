# BetterLootTracker

OriathHub plugin for Path of Exile 2 that tracks currency loot per map and session, with NinjaPricer integration and an in-game HUD overlay.

**Author:** Raff  
**Version:** 1.9.2

## Features

- Per-map and session loot totals with divine / chaos / exalted display
- Best map, last map, and current map tracking
- HUD overlay with sortable currency table (Item | Qty | Price)
- Configurable map value threshold (green / red rating)
- NinjaPricer price matching via game data + league JSON files
- Session history saved on reset

## Requirements

- [OriathHub](https://github.com/danthespal/OriathHubSDK) with a valid license
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [NinjaPricer](https://github.com/danthespal/OriathHubSDK) plugin (for live prices)

## Install (binary)

1. Download `BetterLootTracker.dll` from the repository [Releases](https://github.com/RAcbd/BetterLootTracker/releases) page, or build from source (below).
2. Copy the `BetterLootTracker` folder into your OriathHub `Plugins` directory:
   ```
   Plugins/BetterLootTracker/
     BetterLootTracker.dll
     config/settings.json.example   → copy to settings.json
     data/currency-names.json
   ```
3. Enable the plugin in OriathHub and set your Ninja league in settings.

## Build from source

```powershell
cd src/BetterLootTracker
dotnet restore
dotnet build -c Release
```

The built DLL is at `src/BetterLootTracker/bin/Release/net10.0-windows/BetterLootTracker.dll`.

Copy it to your OriathHub `Plugins/BetterLootTracker/` folder along with `config/` and `data/`.

## Configuration

| File | Purpose |
|------|---------|
| `config/settings.json` | Plugin settings (created on first run; see `config/settings.json.example`) |
| `data/currency-names.json` | Display name overrides and manual Ninja ID links (`byPath`, `byPathNinjaId`) |

Most currencies are priced automatically. If an item shows `—`, add a `byPathNinjaId` entry with the poe.ninja item id, then click **Reload currency names** in the dashboard.

## Development layout

```
BetterLootTracker/
  src/BetterLootTracker/   # C# source
  data/                    # Default currency name mappings
  config/                  # Example settings
  BetterLootTracker.dll    # Pre-built release (optional)
```

## License

MIT — see [LICENSE](LICENSE).
