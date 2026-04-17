# Client Cheat Sheet

Client Cheat Sheet, a client-sided fork of Cheat Sheet for tModLoader. Runs on vanilla multiplayer servers without requiring server-side installation.

## What it does

Provides the cheat toolbars, item browser, recipe browser, and paint tools from Cheat Sheet as a client-only mod. The item-spawn path writes directly to the local inventory and then uses vanilla item sync, so servers see the spawned item as a normal pickup. Features that require server participation (NPC spawn and butcher, vacuum pull, quick teleport, quick clear, spawn-rate control) are stubbed pending a rewire to vanilla NetMessage; see "Known regressions" below.

## Target versions

- Terraria 1.4.4.9
- tModLoader v2026.2.3 or later

## Install

Build from source inside tModLoader:

1. Clone this repo into `Documents/My Games/Terraria/tModLoader/ModSources/CCSheet`.
2. Launch tModLoader, open Workshop, choose Develop Mods, select CCSheet, click Build and Reload.

## Build from source (outside tModLoader)

```
git clone --recurse-submodules --shallow-submodules https://github.com/RobbyV2/CCSheet
cd CCSheet
dotnet build
```

The project references tModLoader assemblies resolved through `tModLoader.targets`, so a tModLoader install must be discoverable at its standard Steam path.

## Known regressions from the stub phase

Seven multiplayer-facing features are currently non-functional pending rewire to vanilla `NetMessage.SendData`:

- NPC spawn (right-click in NPC browser)
- NPC butcher hotbar
- Vacuum pull hotbar
- Quick teleport hotbar
- Quick clear hotbar
- Spawn-rate multiplier
- Server-side packet relay in the main mod class

Item browsing and spawning works end-to-end for vanilla items. Modded items require the other endpoints in the session to have the mod installed that provides those items.

## Maintenance

See [docs/MAINTENANCE.md](docs/MAINTENANCE.md) for the upstream-sync workflow, invariants that must survive identifier sweeps, and the Newtonsoft-to-STJ migration rules.

## Credits

Upstream: Cheat Sheet by jopojelly, https://github.com/jopojelly/CheatSheet, MIT.

The `references/` directory contains submodule pointers to:

- CheatSheet (upstream, MIT)
- QTRHacker (GPL-3.0, reference only, not linked or compiled)
- tModLoader
- TShock
- OTAPI

## License

MIT, inherited from Cheat Sheet. See `LICENSE`.
