# Strategy: ClientCheats Multiplayer Port

## 1. Problem Statement

Goal: operate a client-side fork of Cheat Sheet on vanilla multiplayer servers.

Constraint: no ModPacket may be transmitted; the server must see only vanilla traffic. The server runs no mods; it cannot receive, route, or reply to any tModLoader-specific packet type.

Target: Terraria 1.4.4.9 / tModLoader v2026.3.0.

Current state: scaffold commit `222739a` stubs every ModPacket send site with an Option A early return plus a TODO marker. Features compile cleanly, but any feature that previously relied on ModPacket routing is non-functional in multiplayer until it is rewired to vanilla `NetMessage.SendData` or confirmed to be purely local.

---

## 2. Inputs

- `docs/analysis/cheatsheet_features.md` -- feature inventory with KEEP_AS_IS / REIMPLEMENT_CLIENTSIDE / WORKAROUND_REQUIRED / DROP classifications and per-feature ModPlayer hook analysis.
- `docs/analysis/qtrhacker_techniques.md` -- QTRHacker technique catalog classified as LOCAL_PLAYER_STATE / CRAFTABLE_NETMESSAGE / WORLD_EDIT / PROCESS_EXTERNAL, with GPL risk ratings.
- `docs/analysis/netcode_map.md` -- vanilla MessageID catalog, feature-to-MessageID mapping table, server-rejection flag list, and seven open questions requiring live testing.

---

## 3. Feature Decisions

### 3.1 Keep as-is (identifier rename only)

These features contain no ModPacket calls. Porting them requires only a namespace rename.

| Feature | Rationale |
|---|---|
| Item Browser | All state writes go to `Main.mouseItem` and `QuickSpawnItem`; no network call ever issued. |
| Recipe Browser | Filters `Main.recipe[]` locally; pure display logic. |
| God Mode | `ImmuneTo` and `PreUpdate` hooks run in the client's own player simulation; server sees results via vanilla player sync (MessageID 13). |
| Extra Accessory Slots | `UpdateEquips` applies locally; `SaveData`/`LoadData` target the `.plr` tag file; server never needs accessory slot logic. |
| Minion Slot Booster | Multiplayer sync block was already commented out in upstream; `ChangeSettingLogic` is called directly. |
| Light Hack | `GlobalWall.ModifyLight` is a client-side rendering hook; no other player or server is affected. |
| Clear Buffs | `DelBuff` modifies local `buffType`/`buffTime`; vanilla player sync propagates the change. |
| Clear Debuffs | Same code path as Clear Buffs, filtered to debuff types. |
| Paint Tools: Stamp Place/Undo | Already uses `NetMessage.SendTileSquare` and `sendWater`; no ModPacket in the tile placement path. |
| Paint Tools: Schematic Browser | HTTP fetch and local file I/O only; entirely client-side. |
| Mod Extensions | UI dispatch layer; delegates are provided by third-party mods and are responsible for their own network logic. |
| Hotbar Toggle Keybind | `ProcessTriggers` flips `hotbar.visible`; pure UI state. |
| Configuration Tool | Reads/writes `Main.SavePath/CheatSheetConfig.json`; no network I/O. |

### 3.2 Reimplement client-side (ModPacket stripped; effect is local)

These features sent a ModPacket solely to synchronize a value to a server-side handler, but the actual effect is local. Stripping the send is the complete fix; no replacement packet is needed.

- **Spawn Rate Multiplier**: `EditSpawnRate` is a client-side NPC spawn hook. The ModPacket existed only to broadcast the multiplier index. Fix: call `ChangeSettingLogic(newIndex)` directly on button click; remove the `RequestSetSpawnRate` branch entirely.

### 3.3 Workaround via vanilla NetMessage

These features used ModPacket to route a request through the server for a server-authoritative action. Each can be re-expressed using the vanilla MessageID listed below.

| Feature | Original ModPacket purpose | Replacement MessageID | Implementation sketch | Anticheat / desync risk |
|---|---|---|---|---|
| NPC Browser: Spawn NPC (MPAllowedEnemies types) | `SpawnNPC` routed NPC.NewNPC to server | SpawnBossUseLicenseStartEvent (61) | `NetMessage.SendData(61, -1, -1, null, Main.myPlayer, (float)npcType)` -- server checks `MPAllowedEnemies[type]` | Low for supported types; non-MPAllowedEnemies types are silently dropped (see Section 8). |
| NPC Butcherer | `ButcherNPCs` routed `StrikeNPC` to server | StrikeNPC (28) via `NetMessage.SendStrikeNPC` | Call `StrikeNPC` with `damage = npc.lifeMax` directly; pass `syncData = (Main.netMode == 1)`. Remove `SyncButcher`. | Server forwards damage without a cap in vanilla; TShock may flag implausible damage values (see Section 8). |
| Vacuum Items | `VacuumItems` routed item position updates to server | SyncItem / ItemDrop (21) | Move item position locally, then `NetMessage.SendData(21, -1, -1, null, i, ...)` per item. Remove `SyncVacuum`. | Low; standard item position sync. |
| Clear Dropped Items | `HandleClearItems` routed item clears to server | SyncItem (21) | Call `HandleClearItems(Main.netMode == 1)` directly. Remove `SyncQuickClear` items path. | Low; stack=0 is a normal item-consumed signal. |
| Clear Projectiles | `HandleClearProjectiles` routed projectile kills to server | SyncProjectile (27) / KillProjectile (29) | Call `HandleClearProjectiles(Main.netMode == 1)` directly. Remove `SyncQuickClear` projectile path. Server rejects kills on projectiles the client does not own. | Medium; can only kill own projectiles server-side; foreign projectiles require a workaround or silent drop. |
| Teleport: Dungeon | `TeleportPlayer` routed teleport to server | Teleport (65) style 3 | Change `if (Main.netMode != 2)` guard to `if (Main.netMode != 1)`; remove `SyncTeleport`. Send `NetMessage.SendData(65, -1, -1, null, 0, player.whoAmI, pos.X, pos.Y, 3, 0, 0)`. | Medium; style 3 enforcement needs live confirmation (see Section 8, Q5). |
| Teleport: Spawn | Same as Dungeon; `pos = Main.spawnTile * 16` | Teleport (65) style 3 | Identical fix. | Same as above. |
| Teleport: Hell | Same as Dungeon; `y = (Main.maxTilesY - 200) * 16` | Teleport (65) style 3 | Identical fix. | Same as above. |
| Teleport: Temple | Same as Dungeon; temple coordinates from world data | Teleport (65) style 3 | Identical fix. | Same as above. |
| Teleport: Random | `HandleRandomTeleport` called `TeleportationPotion()` then sent MessageID 65 | Teleport (65) style 3 | Call `HandleRandomTeleport(player, Main.netMode == 1)` directly. Remove `SyncTeleport`. | Same as above. |

### 3.4 Drop

These features are server-authoritative with no viable client-only replacement.

| Feature | Reason |
|---|---|
| NPC Browser: Filter (ban NPCs) | Enforcement hook (`GlobalNPC.PreAI`) must run on the server. No server-side mod means no enforcement; the ban list is meaningless. |
| Boss Downer | Writes `NPC.downedBoss*` flags that are server world state. The next `WorldData` packet (MessageID 7, server-to-client only) overwrites any client-side write. |
| Event Manager | Guarded with `if (Main.netMode != 1)` in upstream; invasion/blood moon state flows exclusively via `WorldData` from the server. |
| Server Config Guard | `ConfigScope.ServerSide` `ModConfig`; requires the server to load the mod. No vanilla server equivalent. |
| NPC Filter Sync on Join | `ModSystem.NetSend`/`NetReceive` requires a server running the same mod. The filter feature itself is dropped, making this sync moot. |

---

## 4. New Techniques from QTRHacker to Adopt

These are LOCAL_PLAYER_STATE and CRAFTABLE_NETMESSAGE items from Agent B that CheatSheet does not already provide. PROCESS_EXTERNAL items are excluded. MEDIUM-GPL_RISK items are listed only where the underlying Terraria API call carries no structural proximity risk; HIGH-GPL_RISK items are omitted entirely.

- **Infinite Oxygen**: `player.breath = player.breathMax` each tick in `ModPlayer.PostUpdate`. Breath state is client-local; no server sync needed. GPL_RISK: LOW.
- **Infinite Ammo**: Override `ModPlayer.CanConsumeAmmo` to return `false`. This is the tModLoader-sanctioned hook; no assembly patching required. GPL_RISK: LOW.
- **Immune to Debuffs**: In `ModPlayer.ResetEffects`, iterate all debuff IDs where `Main.debuff[type] == true` and set `player.buffImmune[type] = true`. GPL_RISK: LOW.
- **Ghost Mode (noclip)**: `player.ghost = true` in `ModPlayer.PostUpdate`. Note: some anti-cheat plugins flag the ghost flag for non-creative players. GPL_RISK: LOW.
- **Slow Fall**: `player.slowFall = true` in `ModPlayer.PostUpdateEquips`. No server sync needed; fall speed is computed client-side. GPL_RISK: LOW.
- **Super Fast Speed**: `player.moveSpeed = 20f` in `ModPlayer.PostUpdateEquips`. Abnormal speed is visible to other players and detectable by anti-speedhack servers. GPL_RISK: LOW.
- **Super Grab Range**: Override `ModPlayer.GrabRange` to return a large integer. Pickup range is checked client-side only. GPL_RISK: LOW.
- **Super Interaction Range**: `player.tileRangeX = 4096; player.tileRangeY = 4096` each tick. Some servers enforce range server-side and silently reject distant tile actions. GPL_RISK: LOW.
- **Fast Tile/Wall Placement**: `player.tileSpeed = 20f; player.wallSpeed = 20f` each tick in `ModPlayer.PostUpdateEquips`. No server enforcement; placement speed is client-side. GPL_RISK: LOW.
- **Enable All Recipes**: In `ModSystem.PostResetRecipes`, set `Main.numAvailableRecipes` to the recipe count and fill `Main.availableRecipe[]` sequentially. Client-side display only; server does not validate recipe eligibility during crafting. GPL_RISK: LOW.
- **Reveal Whole Map**: Loop `Main.Map.UpdateLighting(x, y, 255)` over all tile coordinates; set `Main.refreshMap = true`. Expensive on large worlds; spread over multiple ticks. GPL_RISK: LOW.
- **Right-click Map Teleport**: In `ModSystem.PostUpdateEverything`, detect fullscreen-map open and right-click release; unproject mouse from map space using `Main.mapFullscreenScale` and `Main.mapFullscreenPos`; write `Player.position`; in multiplayer send `NetMessage.SendData(65)` for server acknowledgment. GPL_RISK: LOW.
- **Mechanical Ruler always-on**: `player.rulerGrid = true` each tick. Visual-only; no server interaction. GPL_RISK: LOW.
- **Mechanical Lens always-on**: `player.InfoAccMechShowWires = true` each tick. Visual-only; no server interaction. GPL_RISK: LOW.
- **Auto Fishing Bot**: In `ModPlayer.PreItemCheck`, check for active bobber where `bobber.ai[1] < 0`; inspect `bobber.localAI[1]` sign to determine catch type; set `player.controlUseItem` and `player.releaseUseItem` then call `player.ItemCheck()` to reel in and re-cast. GPL_RISK: LOW.
- **Player Properties Editor**: UI panel exposing `player.statLifeMax`, `player.statManaMax`, and cosmetic color fields for direct assignment. All fields are public. Sync HP/mana via MessageID 16 / 42 in multiplayer. GPL_RISK: LOW.
- **Inventory Save and Load**: Binary serialize `(int type, int stack, byte prefix)` per slot to a file; restore via `item.SetDefaults(type)` + `item.Prefix(prefix)` + `item.stack = stack`. Schema is independently designed; no reference to QTRHacker's binary layout. GPL_RISK: LOW.
- **Apply NPC Buff**: Loop `Main.npc[]`; for each active entry call `npc.AddBuff(type, time)`; in multiplayer send `NetMessage.SendData(MessageID.NPCAddBuff, ...)`. Multiplayer server may drop `NPCAddBuff` from clients; treat as best-effort. GPL_RISK: LOW.

---

## 5. Architecture

**Local state layer**: `ClientCheatsPlayer` (`ModPlayer`) holds per-player cheat flags and override values. Module-local static fields carry UI state. This mirrors CheatSheet's existing pattern.

**UI layer**: Ported unchanged from CheatSheet after the identifier sweep that handles the namespace rename. No UI logic is multiplayer-aware.

**Netcode layer**: No `ModPacket`. A single thin helper class (e.g., `VanillaSync`) wraps `NetMessage.SendData` calls for all rewired features. This centralizes the `if (Main.netMode == 1)` guards and makes future auditing straightforward. All calls in this helper are one-liners delegating to `NetMessage.SendData`.

**HandlePacket**: Stub retained in the scaffold. No receive logic is implemented. Reserved for a future scenario where a companion tML server-side module is added.

---

## 6. Milestone Plan

### M1 -- Item Spawning and Inventory Sync

Features covered: Item Browser (KEEP_AS_IS, A-ID: Item Browser), inventory slot writes.

MessageIDs used: SyncEquipment (5) for giving self items into inventory slots; SyncItem/ItemDrop (21) for world-drop items and Vacuum Items.

Work: verify on a live vanilla server that `NetMessage.SendData(5, ...)` with `whoAmI = Main.myPlayer` and an arbitrary `netID` and `prefix` succeeds and is visible to other players. Verify Vacuum Items (`HandleClearItems` path) works with the `SyncVacuum` stub removed.

Open questions: Q4 (TileSquareSync size limit) is adjacent; preliminary tile-sync sanity check is appropriate here.

### M2 -- God Mode, Buffs, Infinite Resources

Features covered: God Mode (KEEP_AS_IS re-confirmed), Clear Buffs/Clear Debuffs (KEEP_AS_IS), plus new QTRHacker additions: Infinite Ammo, Infinite Oxygen, Immune to Debuffs.

MessageIDs used: AddPlayerBuff (55) for self-buff grants; PlayerBuffs (50) for buff array sync; PlayerLife (16) for HP assertion in multiplayer.

Work: confirm `AddPlayerBuff (55)` with `whoAmI = Main.myPlayer` applies and is acknowledged by a vanilla server. Add `CanConsumeAmmo` and `buffImmune` hooks to `ClientCheatsPlayer`. Wire `OnConsumeMana` override for infinite mana (or `statMana` assignment each tick via `PostUpdate`).

Open questions: Q3 -- whether AddPlayerBuff (55) accepts only own-slot `whoAmI` or arbitrary indices.

### M3 -- Teleport and Movement

Features covered: Teleport Dungeon, Spawn, Hell, Temple, Random (five WORKAROUND_REQUIRED features); new QTRHacker additions: Super Fast Speed, Ghost Mode, Slow Fall, Super Grab Range, Right-click Map Teleport.

MessageIDs used: Teleport (65) style 3.

Work: change `if (Main.netMode != 2)` to `if (Main.netMode != 1)` in `RunTeleport`; remove `SyncTeleport` and the `TeleportPlayer` message type. Test all five destination variants on a live vanilla server. Add movement flags to `ClientCheatsPlayer.PostUpdateEquips`.

Open questions: Q5 -- whether Teleport (65) style 3 requires the Rod of Discord in inventory on a modern vanilla server.

### M4 -- NPC Butcher and Spawn

Features covered: NPC Butcherer (WORKAROUND_REQUIRED), NPC Browser: Spawn NPC (WORKAROUND_REQUIRED for MPAllowedEnemies types).

MessageIDs used: StrikeNPC (28) via `NetMessage.SendStrikeNPC`; SpawnBossUseLicenseStartEvent (61).

Work: remove `SyncButcher`; call `StrikeNPC` with `damage = npc.lifeMax` directly, guarded by `syncData = (Main.netMode == 1)`. For NPC spawn, replace `SyncNPC` path with `NetMessage.SendData(61, ...)` for MPAllowedEnemies types; surface a clear UI note that arbitrary NPC types are unavailable in vanilla multiplayer.

Open questions: Q1 -- whether MessageID 61 checks item possession; Q2 -- whether StrikeNPC (28) enforces a damage cap or proximity requirement.

### M5 -- UI Parity, Remaining KEEP_AS_IS Features, and Documentation Pass

Features covered: all KEEP_AS_IS features verified post-rename; new QTRHacker quality-of-life additions (Enable All Recipes, Reveal Whole Map, Auto Fishing Bot, Player Properties Editor, Inventory Save and Load, Mechanical Ruler/Lens, Fast Tile/Wall Placement, Super Interaction Range).

MessageIDs used: none new; SyncEquipment (5) for inventory editor MP sync; PlayerLife (16) and PlayerMana (42) for Player Properties Editor MP sync.

Work: run a full identifier sweep audit to confirm no upstream `CheatSheet.*` namespace references remain. Verify each KEEP_AS_IS feature on a live server. Add QTRHacker-sourced features to `ClientCheatsPlayer` and wire them into the UI. Documentation pass: update each subdirectory `CLAUDE.md` file.

Open questions: Q6 -- whether PlayerBuffs (50) with another player's `whoAmI` is forwarded by the server. Q7 -- confirmed integer value of SpawnBossUseLicenseStartEvent in the active tModLoader build.

---

## 7. Risks

**Server anticheat detection**: TShock and similar plugins log `StrikeNPC` (28) damage values, `Teleport` (65) coordinates, and `PlayerControls` (13) velocity fields. Values that exceed plausible in-game ranges (e.g., `npc.lifeMax` damage for a boss, teleporting 10,000 tiles in one packet) are commonly flagged. The workaround strategy intentionally uses vanilla MessageIDs, but implausible payloads remain detectable.

**Desync between local predicted state and server state**: Locally setting `player.statLife`, `player.ghost`, or movement fields takes effect on the client immediately. If the server's next sync packet contradicts the local value, the client state snaps back. Features like God Mode (`statLife = statLifeMax` each tick) and Infinite Fly Time (`wingTime` assignment each tick) tolerate this by re-asserting every tick; single-shot changes (teleport, item set) do not.

**GPL contamination from QTRHacker**: QTRHacker is GPL-3.0. No source code is copied; this project's adoption is limited to the technique descriptions in `docs/analysis/qtrhacker_techniques.md`, which are a prose-only study of publicly observable behavior against the Terraria public API. All implementations are written independently against `ModPlayer` and `NetMessage` interfaces.

**tModLoader API drift**: The CheatSheet submodule was last tested against a tModLoader version that may pre-date v2026.3.0. Known potential breakages include: `ModKeybind` vs `Hotkey` API rename, UI element constructors and `ProcessTriggers` signature changes, and `ModSystem.PostResetRecipes` hook availability. Each M5 feature adoption should be preceded by a compilation check against the targeted tML build.

---

## 8. Open Questions

These require live multiplayer testing against a vanilla Terraria 1.4.4.9 server. Source: `docs/analysis/netcode_map.md` Section 4.

1. Whether SpawnBossUseLicenseStartEvent (61) with a positive NPC type value checks that the sending client's inventory contains the matching summon item, or only checks `NPCID.Sets.MPAllowedEnemies[type]`. Affects M4 NPC spawn.

2. Whether StrikeNPC (28) on a vanilla server enforces a maximum damage cap or a proximity requirement. CheatSheet sends `npc.lifeMax` as damage without clamping. Affects M4 NPC butcher reliability.

3. Whether AddPlayerBuff (55) restricts the `whoAmI` field to the sender's own slot only, or accepts arbitrary player indices (which would enable forced-buffing other players). Affects M2 buff grants.

4. Whether TileSquareSync (20) enforces a maximum patch size server-side. Affects Paint Tools large-schematic placement and any mass tile-clear operations.

5. Whether Teleport (65) style 3 on a modern vanilla server requires the Rod of Discord in the player's inventory, or forwards any style-3 teleport unconditionally. The current CheatSheet teleport path ran under a server-relayed ModPacket and did not go through this vanilla path. Affects M3 entirely.

6. Whether PlayerBuffs (50) sent with a `whoAmI` value equal to another player's index is forwarded by the server, enabling forced debuff manipulation on other players.

7. The exact integer value of SpawnBossUseLicenseStartEvent in the active tModLoader v2026.3.0 `MessageID` enum. Reference code uses the named constant; the underlying integer should be confirmed to remain 61.

---

## 9. Licensing Posture

- **ClientCheats**: MIT, inherited from CheatSheet upstream.
- **CheatSheet upstream**: MIT.
- **QTRHacker**: GPL-3.0. No verbatim source is copied into this project. All QTRHacker-derived technique descriptions are a prose-only reference study; implementations are written independently against the tModLoader and Terraria public APIs.
- **tModLoader / TShock / OTAPI**: added as reference submodules for protocol and server-behavior reference only; their licenses govern their own files and do not propagate to ClientCheats source.
