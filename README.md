# Client Cheat Sheet (CCSheet)

A client-sided soft-fork of [Cheat Sheet](https://github.com/jopojelly/CheatSheet) for tModLoader. CCSheet runs on vanilla multiplayer servers without requiring server-side installation; all features are client-local or routed through vanilla `NetMessage` packets that any vanilla tML server accepts.

## Target versions

- Terraria 1.4.4.9
- tModLoader v2026.2.3 or later

## Current version

1.0.2 — see `changelog.txt`.

## What it does

Inherits the full Cheat Sheet feature set (item browser, NPC browser, recipe browser, paint tools, quick teleport, quick clear, NPC butcher, vacuum, extra accessory slots, spawn rate multiplier, light hack, god mode, mod extension cheats) and adds CCSheet-specific tooling on top.

### CCSheet-specific additions

- **Item Editor** (1.0.2): a floating window opened from the Actuation Rod button on the main hotbar. Place an item in the slot and edit its stats (type, stack, prefix, damage, crit, knockback, use time, use animation, shoot, shoot speed, mana, scale, ammo, use ammo, auto reuse, rare) with an Apply button that commits changes to the item instance. Taking the item out clears the fields.
- **Extra Accessory Slots up to 20** (1.0.2): the cycle button now goes to +20. Slots wrap into a second column 47 px left once the first column matches the vanilla right-side UI height.
- **MP waypoint warnings** (1.0.1): in multiplayer, a one-time notice explains that teleports can flash through unloaded terrain. Dungeon, hell, and temple teleports warn in chat when their target tiles are not synced to the client (instead of landing you off-map or silently failing).
- **Spawn Rate Multiplier hidden in MP** (1.0.1): this setting is inherently server-side and has no client-side effect, so the button is hidden on multiplayer clients.
- **Stubbed features rewired to vanilla netcode** (1.0.1): NPC spawn, NPC butcher, vacuum pull, quick teleport, and quick clear now work on vanilla servers using standard vanilla `NetMessage.SendData` calls (no custom server-side handler required).

### Multiplayer nuances to be aware of

- **NPC spawning**: bosses spawn via `MessageID.SpawnBossUseLicenseStartEvent`, which vanilla servers accept because it is the same packet the Suspicious Looking Eye and similar summoning items use. Regular non-boss enemies are spawned client-side via `NPC.NewNPC`, which in vanilla MP only spawns on the local client and does not replicate to peers (vanilla does not grant clients authority to spawn arbitrary enemies). Expect bosses to work in MP; expect regular enemies to appear only for you.
- **Quick Teleport waypoints**: the first teleport in an MP session shows a one-time notice because vanilla netcode streams tile data after the teleport, so you may briefly flash through unloaded terrain. Dungeon teleport requires `Main.dungeonX`/`Y` to be set on the client, which vanilla servers do not broadcast; the button warns instead of landing you at (0, 0). Hell and temple require their target tiles to have been streamed to the client, typically by having walked to those areas at least once.
- **Quick Clear**: clears dropped items, active projectiles, buffs, and debuffs. In MP, item and projectile clears use `NetMessage` 21 and 27 respectively to replicate. Buff clears are local to your player.
- **Extra Accessory Slots**: purely client-local. Stats apply locally; the server does not know or care about the additional slots. Untested in heavy mod-stack MP configurations.

## Install

Build from source inside tModLoader:

1. Clone this repo into `Documents/My Games/Terraria/tModLoader/ModSources/CCSheet`.
2. Launch tModLoader, open Workshop, choose Develop Mods, select CCSheet, click Build and Reload.

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
