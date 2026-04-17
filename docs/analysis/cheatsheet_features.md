# CheatSheet Feature Inventory

Port strategy: replace `ModPacket` netcode with local state and vanilla `NetMessage.SendData` calls.

Classifications:
- `KEEP_AS_IS`: pure client-side, no changes needed
- `REIMPLEMENT_CLIENTSIDE`: has ModPacket today but effect is local; strip the packet
- `WORKAROUND_REQUIRED`: uses ModPacket for server-state effect; reimplement via vanilla `NetMessage.SendData`
- `DROP`: fundamentally server-dependent or admin-only

---

## Section 1: Summary Table

| Feature | File(s) | Uses ModPacket? | State touched | Classification | Rationale |
|---------|---------|-----------------|---------------|----------------|-----------|
| Item Browser | Menus/ItemBrowser.cs, Menus/Slot.cs | No | Main.mouseItem (client), QuickSpawnItem (client) | KEEP_AS_IS | All state changes are local; no packet ever sent |
| Recipe Browser | Menus/RecipeBrowser.cs | No | Recipe display state (client) | KEEP_AS_IS | Pure display filter; reads vanilla recipe list locally |
| God Mode | Menus/GodMode.cs | No | Player.statLife, statMana, wingTime (client ModPlayer) | KEEP_AS_IS | ImmuneTo and PreUpdate hooks run entirely client-side |
| Extra Accessory Slots | CheatSheetPlayer.cs | No | Player equip slots, saved to player tag (client) | KEEP_AS_IS | UpdateEquips applies buffs locally; SaveData persists to player file |
| Minion Slot Booster | Menus/MinionSlotBooster.cs | No | Player.maxMinions (client PostUpdateEquips) | KEEP_AS_IS | Netcode commented out; ChangeSettingLogic called directly |
| Light Hack | Menus/LightHack.cs | No | Tile lighting r/g/b offset (client render) | KEEP_AS_IS | GlobalWall.ModifyLight is a client-side rendering hook |
| Clear Buffs | Menus/QuickClearHotbar.cs | No | Player.buffType/buffTime arrays (client) | KEEP_AS_IS | HandleClearBuffs zeroes local buff arrays; no sync needed |
| Clear Debuffs | Menus/QuickClearHotbar.cs | No | Player.buffType/buffTime arrays (client) | KEEP_AS_IS | Same path as Clear Buffs; filters debuffs locally |
| Paint Tools: Stamp Place/Undo | Menus/PaintToolsHotbar.cs | No | Main.tile[x,y] + NetMessage.SendTileSquare | KEEP_AS_IS | Already uses vanilla SendTileSquare and sendWater; no ModPacket |
| Paint Tools: Schematic Browser | Menus/PaintToolsHotbar.cs | No | HTTP WebClient, local TileData files | KEEP_AS_IS | Online schematic fetch is HTTP; local saves are file I/O |
| Mod Extensions | Menus/ExtendedCheatMenu.cs | No | Action delegates (third-party) | KEEP_AS_IS | Pure UI dispatch; delegates registered by other mods |
| Hotbar Toggle Keybind | CheatSheetPlayer.cs | No | Hotbar.visible (client UI state) | KEEP_AS_IS | ProcessTriggers reads keybind and flips a bool |
| Configuration Tool | Menus/ConfigurationTool.cs, Configuration.cs | No | PersonalConfiguration JSON on disk | KEEP_AS_IS | Reads/writes local JSON at Main.SavePath; entirely client-side |
| Spawn Rate Multiplier | Menus/SpawnRateMultiplier.cs | Yes | currentMultiplier float (client GlobalNPC.EditSpawnRate) | REIMPLEMENT_CLIENTSIDE | EditSpawnRate runs on client; ModPacket only syncs index to server for broadcast; strip packet and apply locally |
| NPC Browser: Spawn NPC | Menus/NPCSlot.cs | Yes | NPC.NewNPC on server (world state) | WORKAROUND_REQUIRED | SyncNPC sends SpawnNPC packet; replace with NetMessage.SendData(51) or direct NPC spawn via vanilla packet |
| NPC Butcherer | Menus/NPCButchererHotbar.cs | Yes | NPC health on server via StrikeNPC | WORKAROUND_REQUIRED | SyncButcher packet triggers HandleButcher; replace with NetMessage.SendStrikeNPC client-side directly |
| Vacuum Items | Menus/Hotbar.cs | Yes | Main.item[i].position (world item state) | WORKAROUND_REQUIRED | SyncVacuum triggers VacuumItems; port: move item locally then NetMessage.SendData(21) per item |
| Clear Dropped Items | Menus/QuickClearHotbar.cs | Yes | Main.item[i] cleared (world item state) | WORKAROUND_REQUIRED | HandleClearItems zeros items then sends SendData(21); strip ModPacket, call SendData(21) directly |
| Clear Projectiles | Menus/QuickClearHotbar.cs | Yes | Projectile.active (server projectile state) | WORKAROUND_REQUIRED | HandleClearProjectiles kills and sends SendData(27); strip ModPacket, call SendData(27) directly |
| Teleport: Dungeon | Menus/QuickTeleportHotbar.cs | Yes | Player.position (server-visible) | WORKAROUND_REQUIRED | RunTeleport guard is netMode!=2; in port: Teleport locally then SendData(65) when netMode==1 |
| Teleport: Spawn | Menus/QuickTeleportHotbar.cs | Yes | Player.position | WORKAROUND_REQUIRED | Same as Dungeon teleport; same fix applies |
| Teleport: Hell | Menus/QuickTeleportHotbar.cs | Yes | Player.position | WORKAROUND_REQUIRED | Same as Dungeon teleport; same fix applies |
| Teleport: Temple | Menus/QuickTeleportHotbar.cs | Yes | Player.position | WORKAROUND_REQUIRED | Same as Dungeon teleport; same fix applies |
| Teleport: Random | Menus/QuickTeleportHotbar.cs | Yes | Player.position | WORKAROUND_REQUIRED | HandleRandomTeleport calls TeleportationPotion then SendData(65); strip ModPacket, guard on netMode==1 |
| NPC Browser: Filter (ban NPCs) | Menus/NPCSlot.cs, CheatSheetNPC.cs | Yes | NPCBrowser.filteredNPCSlots (server world state) | DROP | Filter is enforced by GlobalNPC.PreAI on server; client-only mod cannot set server NPC bans |
| Boss Downer | Menus/BossDowner.cs | No | NPC.downedBoss* flags (server world state) | DROP | Commented out of hotbar; writes world flags directly; meaningless without server-side mod |
| Event Manager | Menus/EventManagerHotbar.cs | No | Invasion/bloodmoon world state | DROP | Commented out of SetupUI; invasion writes have netMode!=1 guards making them no-ops on client |
| Server Config Guard | CheatSheetServerConfig.cs, CheatSheetWorld.cs | No | DisableCheatsForNonHostUsers (server config) | DROP | ConfigScope.ServerSide; only meaningful with server-side mod installed |
| NPC Filter Sync on Join | CheatSheetWorld.cs | Yes (ModSystem.NetSend) | NPCBrowser.filteredNPCSlots sync | DROP | NetSend/NetReceive requires server-side mod; without it no filter state exists to sync |

---

## Section 2: Per-Feature Detail

### Item Browser

**File(s):** `Menus/ItemBrowser.cs`, `Menus/Slot.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`Slot2_onLeftClick` sets `Main.mouseItem.netDefaults(item.netID)` and `stack = maxStack` locally. Shift-click calls `Main.LocalPlayer.QuickSpawnItem` which is a legitimate client-side action. Right-click drag-splits the stack via `Main.mouseItem.stack++` with no network calls. The browser itself filters `ContentSamples.ItemsByType` locally. Zero network dependency.

---

### Recipe Browser

**File(s):** `Menus/RecipeBrowser.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

Iterates `Main.recipe[]` to build a display list. Filtering and search are purely local operations on already-loaded recipe data. No server state is read or written.

---

### God Mode

**File(s):** `Menus/GodMode.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`GodModeModPlayer.ImmuneTo` returns `true` for all damage sources. `PreUpdate` sets `statLife = statLifeMax2`, `statMana = statManaMax2`, and `wingTime = wingTimeMax` each tick. These are `ModPlayer` hooks that run in the client's own player simulation; the server sees the resulting health values via normal player sync (`NetMessage.SendData(13)`), which vanilla already handles.

---

### Extra Accessory Slots

**File(s):** `CheatSheetPlayer.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

Maintains `ExtraAccessories[]` array and `numberExtraAccessoriesEnabled`. `UpdateEquips` and `ApplyEquipFunctional` apply prefix and armor benefits locally. `SaveData`/`LoadData` persist slots to the player's `.plr` tag file. The server never needs to know about extra accessory logic; only the player's resulting stats matter, which propagate via vanilla sync.

---

### Minion Slot Booster

**File(s):** `Menus/MinionSlotBooster.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`PostUpdateEquips` increments `Player.maxMinions` by `currentBoost`. The multiplayer network code block was fully commented out in the reference source; `ChangeSettingLogic` is called directly on button click. No packets sent.

---

### Light Hack

**File(s):** `Menus/LightHack.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`LightHackGlobalWall.ModifyLight` adds `brightnessOffset` to the `r`, `g`, `b` output parameters during the client-side tile lighting pass. This is a pure rendering hook; other players are unaffected and the server has no visibility into client lighting.

---

### Clear Buffs

**File(s):** `Menus/QuickClearHotbar.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`HandleClearBuffs` iterates `player.buffType` and calls `player.DelBuff(i)` for non-permanent buffs. This modifies local player state that vanilla sync (`SendData(13)`) propagates automatically.

---

### Clear Debuffs

**File(s):** `Menus/QuickClearHotbar.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

Same code path as Clear Buffs but filtered to debuff types. Same vanilla-sync argument applies.

---

### Paint Tools: Stamp Place and Undo

**File(s):** `Menus/PaintToolsHotbar.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

Writes `Main.tile[x,y]` directly, then calls `NetMessage.SendTileSquare(-1, cx, cy, size)` for tile sync and `NetMessage.sendWater(i, j)` for liquid sync. These are the standard vanilla tile-change network calls. No ModPacket anywhere in the tile placement path.

---

### Paint Tools: Schematic Browser

**File(s):** `Menus/PaintToolsHotbar.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`WebClient.UploadValuesAsync` fetches schematic lists from an external HTTP endpoint. Local schematics are read from and written to disk under `Main.SavePath`. Both paths are entirely client-side I/O.

---

### Mod Extensions

**File(s):** `Menus/ExtendedCheatMenu.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`RegisterButton(Mod, Func<UIImage>, ...)` stores `Action` delegates in a list. On click, the stored delegate is invoked. This is purely a UI dispatch layer; the mod providing the delegate is responsible for any network logic.

---

### Hotbar Toggle Keybind

**File(s):** `CheatSheetPlayer.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`ProcessTriggers` checks `ToggleCheatSheetHotbarHotKey.JustPressed` and flips `hotbar.visible`. Pure client UI state.

---

### Configuration Tool

**File(s):** `Menus/ConfigurationTool.cs`, `Configuration.cs`
**Uses ModPacket:** No
**Classification:** KEEP_AS_IS

`PersonalConfiguration` is a 14-bool POCO serialized to `Main.SavePath/CheatSheetConfig.json`. `ConfigurationLoader.SaveSetting` writes the file. No network I/O; this is per-client preference storage.

---
### Spawn Rate Multiplier

**File(s):** Menus/SpawnRateMultiplier.cs
**Uses ModPacket:** Yes (SetSpawnRate, SpawnRateSet)
**Classification:** REIMPLEMENT_CLIENTSIDE

EditSpawnRate in SpawnRateMultiplierGlobalNPC modifies the spawnRate and maxSpawns ref parameters. This hook runs on whichever machine is running NPC spawn logic, which in vanilla multiplayer is the client for its own spawn calculations. The ModPacket flow exists only to propagate the multiplier index from a client to the server and back to all clients so every session uses the same value. In a client-only mod on a vanilla server, there is no server-side mod to receive the packet. Fix: call ChangeSettingLogic(newIndex) directly on button click, removing the RequestSetSpawnRate path.

**Port change:** In buttonLogic, remove the if (Main.netMode == 1) branch that calls RequestSetSpawnRate; always call ChangeSettingLogic(newIndex) directly.

---

### NPC Browser: Spawn NPC

**File(s):** Menus/NPCSlot.cs
**Uses ModPacket:** Yes (SpawnNPC)
**Classification:** WORKAROUND_REQUIRED

HandleNPC(npcType, netID, syncData) calls SpawnNPC when syncData is false (singleplayer) or defers to SyncNPC which sends a SpawnNPC ModPacket. The server receives the packet and calls NPC.NewNPC because NPC spawning must originate from the authority. A vanilla server has no mod packet handler.

**Port change:** Use NetMessage.SendData(MessageID.SpawnBossUseLicenseAtPlayer, -1, -1, null, Main.myPlayer, npcType) for boss/event NPCs. For non-boss NPCs, call NPC.NewNPC on the client then NetMessage.SendData(23, -1, -1, null, npc.whoAmI) to sync.

---

### NPC Butcherer

**File(s):** Menus/NPCButchererHotbar.cs
**Uses ModPacket:** Yes (ButcherNPCs)
**Classification:** WORKAROUND_REQUIRED

ButcherNPCs(butcherType, syncData) calls npc.StrikeNPC(hit) locally then NetMessage.SendStrikeNPC(npc, hit) when syncData is true. The ModPacket wrapper routes the initial request through the server. In the port, skip the ModPacket: call NPC.StrikeNPC with InstantKill = true directly on the client, then call NetMessage.SendStrikeNPC to notify the server.

**Port change:** In ButcherNPCs, always pass syncData = (Main.netMode == 1). Remove SyncButcher.

---

### Vacuum Items

**File(s):** Menus/Hotbar.cs
**Uses ModPacket:** Yes (VacuumItems)
**Classification:** WORKAROUND_REQUIRED

VacuumItems(syncData, whoAmI) sets Main.item[i].position to the player position and calls NetMessage.SendData(21, ...) per item. The ModPacket wrapper routes the request through the server. In the port, call VacuumItems directly with syncData = (Main.netMode == 1).

**Port change:** Remove SyncVacuum. Call VacuumItems(Main.netMode == 1, Main.myPlayer) on button click.

---

### Clear Dropped Items

**File(s):** Menus/QuickClearHotbar.cs
**Uses ModPacket:** Yes (QuickClear)
**Classification:** WORKAROUND_REQUIRED

HandleClearItems(syncData) zeroes item fields and calls NetMessage.SendData(21, ...) per cleared item. The ModPacket routes the request server-side. In the port, execute the clear locally and call SendData(21) directly.

**Port change:** On button click, call HandleClearItems(Main.netMode == 1) directly. Remove SyncQuickClear for the items path.

---

### Clear Projectiles

**File(s):** Menus/QuickClearHotbar.cs
**Uses ModPacket:** Yes (QuickClear)
**Classification:** WORKAROUND_REQUIRED

HandleClearProjectiles(syncData) calls projectile.Kill() and NetMessage.SendData(27, ...). Same pattern as Clear Dropped Items.

**Port change:** Call HandleClearProjectiles(Main.netMode == 1) directly on button click. Remove the shared SyncQuickClear dispatch once both are ported.

---

### Teleport: Dungeon

**File(s):** Menus/QuickTeleportHotbar.cs
**Uses ModPacket:** Yes (TeleportPlayer)
**Classification:** WORKAROUND_REQUIRED

RunTeleport(player, pos, syncData) calls player.Teleport(pos, 2) then has if (Main.netMode != 2) return; before the NetMessage.SendData(65, ...) call. That guard means SendData(65) only fires when the code runs on a server. In the port, change the guard to if (Main.netMode == 1) and remove the ModPacket routing. The client calls player.Teleport(pos, 2) locally then sends NetMessage.SendData(65, -1, -1, null, 0, player.whoAmI, pos.X, pos.Y, 3, 0, 0) to inform the server.

**Port change:** In RunTeleport, replace if (Main.netMode != 2) return; with if (Main.netMode != 1) return;. Remove SyncTeleport and the TeleportPlayer message type.

---

### Teleport: Spawn

**File(s):** Menus/QuickTeleportHotbar.cs
**Uses ModPacket:** Yes (TeleportPlayer)
**Classification:** WORKAROUND_REQUIRED

Same RunTeleport path as Dungeon teleport with pos = new Vector2(Main.spawnTileX, Main.spawnTileY) * 16. Identical fix.

---

### Teleport: Hell

**File(s):** Menus/QuickTeleportHotbar.cs
**Uses ModPacket:** Yes (TeleportPlayer)
**Classification:** WORKAROUND_REQUIRED

Same RunTeleport path as Dungeon teleport with y coordinate set to (Main.maxTilesY - 200) * 16. Identical fix.

---

### Teleport: Temple

**File(s):** Menus/QuickTeleportHotbar.cs
**Uses ModPacket:** Yes (TeleportPlayer)
**Classification:** WORKAROUND_REQUIRED

Same RunTeleport path with temple coordinates sourced from world dungeon/temple location data. Identical fix.

---

### Teleport: Random

**File(s):** Menus/QuickTeleportHotbar.cs
**Uses ModPacket:** Yes (TeleportPlayer)
**Classification:** WORKAROUND_REQUIRED

HandleRandomTeleport(player, syncData) calls player.TeleportationPotion() then when syncData is true sends NetMessage.SendData(65, ...). TeleportationPotion() handles the position calculation. In the port, call HandleRandomTeleport(player, Main.netMode == 1) directly.

**Port change:** Remove SyncTeleport path for random teleport. Call HandleRandomTeleport directly with syncData = (Main.netMode == 1).

---

### NPC Browser: Filter (Ban NPCs)

**File(s):** Menus/NPCSlot.cs, CheatSheetNPC.cs
**Uses ModPacket:** Yes (RequestFilterNPC, InformFilterNPC)
**Classification:** DROP

The filter is enforced in CheatSheetNPC.GlobalNPC.PreAI, which calls npc.StrikeNPC with InstantKill = true when the NPC type is in filteredNPCSlots. This hook runs on the server or host. A client-only mod cannot hook into server NPC processing. The ModPacket flow (RequestFilterNPC -> server FilterNPC -> InformFilterNPC broadcast) requires the server to be running the mod. Without server presence, the filter list has no enforcement mechanism. The client-side filteredNPCSlots display toggle (greying out) could be kept as a local visual hint, but it has no gameplay effect.

---

### Boss Downer

**File(s):** Menus/BossDowner.cs
**Uses ModPacket:** No
**Classification:** DROP

Commented out of hotbar setup. Writes NPC.downedBoss1, NPC.downedBoss2, etc. directly, which are server world-state flags. On a vanilla server these client-side writes are overwritten by the next world state sync from the server (NetMessage.SendData(7)). No client-only workaround exists for setting permanent world flags without server cooperation.

---

### Event Manager

**File(s):** Menus/EventManagerHotbar.cs
**Uses ModPacket:** No
**Classification:** DROP

Commented out of SetupUI entirely. Invasion and blood moon writes have if (Main.netMode != 1) guards that make them no-ops when running as a multiplayer client. Even if uncommented, the underlying world-state writes (Main.invasionType, Main.bloodMoon) would be overwritten by server sync.

---

### Server Config Guard

**File(s):** CheatSheetServerConfig.cs, CheatSheetWorld.cs
**Uses ModPacket:** No
**Classification:** DROP

CheatSheetServerConfig is a ConfigScope.ServerSide ModConfig. It requires the server to load the mod and serve config values. CheatSheetWorld.UpdateUI and ModifyInterfaceLayers gate all UI rendering on !DisableCheatsForNonHostUsers. In a vanilla server environment the config object never exists on the client. The entire permission-gating mechanism is irrelevant without a server-side mod install.

---

### NPC Filter Sync on Join

**File(s):** CheatSheetWorld.cs
**Uses ModPacket:** Yes (ModSystem.NetSend/NetReceive)
**Classification:** DROP

ModSystem.NetSend serializes NPCBrowser.filteredNPCSlots and NetReceive deserializes it when a player joins. This world-join state sync is part of the tModLoader protocol and requires the server to be running the same mod version. On a vanilla server, no mod packet handling exists. Even if it did, the NPC filter feature itself is dropped (see above), making this sync moot.
