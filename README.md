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
  *.cs                 # plugin source
  BetterLootTracker.csproj
  OriathPlugins.Common/  # shared library (compiled + merged into BetterLootTracker.dll on Release)
  build/               # ILRepack merge targets
```

`config/settings.json` and `data/currency-names.json` are created automatically on first run. Default currency names are embedded in the DLL.

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) and [OriathHub SDK 0.10.1+](https://github.com/danthespal/OriathHubSDK). Marketplace supplies the SDK when building from source.

```powershell
dotnet build BetterLootTracker.csproj -c Release
```

Release output is a **single** `bin/Release/net10.0-windows/BetterLootTracker.dll`.

## License

MIT — see [LICENSE](LICENSE).
