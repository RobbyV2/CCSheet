# Vanilla Netcode Map: Client-Authoritative MessageIDs for ClientCheats

## Section 1: Vanilla MessageID Catalog

All MessageID names are taken exactly from Terraria.ID.MessageID as exposed by tModLoader.

| MessageID name | Value | Key payload fields | Direction | Authority | Client-craftable? | Abuse vector |
|---|---|---|---|---|---|---|
| PlayerControls | 13 | whoAmI (byte), position (Vector2), velocity (Vector2), control flags (byte) | C->S->C | Client-authoritative for sender slot; server forwards without position validation | Yes | Teleport by setting position to any world coordinate; noclip by setting velocity |
| SyncPlayer | 4 | whoAmI (byte), character appearance/stats fields | C->S->C | Client-authoritative for own slot; server forwards | Yes | Rename self, spoof appearance; does not alter gameplay stats persisted server-side |
| SyncEquipment | 5 | whoAmI (byte), slot index (byte), Item fields (netID, stack, prefix) | C->S->C | Client-authoritative for own slot | Yes | Spawn any item into own inventory slot; set prefix to any value; set stack beyond normal maximums |
| PlayerLife | 16 | whoAmI (byte), statLife (short), statLifeMax (short) | C->S->C | Client-authoritative for own slot; server forwards | Yes | Set own HP to any value including above the vanilla cap |
| PlayerMana | 42 | whoAmI (byte), statMana (short), statManaMax (short) | C->S->C | Client-authoritative for own slot; server forwards | Yes | Set own mana to any value |
| PlayerBuffs | 50 | whoAmI (byte), array of 22 buffType (ushort) values | C->S->C | Client-authoritative for own slot; server forwards | Yes | Grant self any buff; remove debuffs; extend duration by resending |
| AddPlayerBuff | 55 | whoAmI (byte), buffType (ushort), time (int) | C->S | Server applies buff and rebroadcasts; no restriction on which buff is requested for own slot | Yes (own slot) | Grant self any buff |
| ItemDrop | 21 | item index (short), position (Vector2), velocity (Vector2), stack (short), prefix (byte), nodeID (byte), netID (short) | C->S->C | Server assigns item slot on new spawns; client can update position/velocity for items it owns | Partial | Client sending value 21 with a valid item index updates position; stack 0 clears the item |
| ItemOwner | 22 | item index (short), whoAmI (byte) | S->C | Server-authoritative | No | Client cannot forge ownership assignment |
| SyncItem | 21 | Same as ItemDrop; alias for value 21 in tModLoader MessageID | S->C and C->S | Server-authoritative for new item slot | Partial | Same as ItemDrop |
| InstancedItem | 102 | destination whoAmI (byte), item fields | S->C only | Server-authoritative; journey mode giving | No | Cannot be initiated from a vanilla-mode client |
| TileManipulation | 17 | action (byte), tileX (short), tileY (short), type (short), style (short) | C->S->C | Server validates coords and reach; reach is not strictly enforced | Yes | Place or break tiles at extended range; action 0=break, 1=place |
| TileSquareSync | 20 | tileX (short), tileY (short), size (byte/short), tile data blob | C->S->C | Server applies the square with limited validation | Yes | Overwrite a region of tiles with arbitrary tile types |
| SpawnBossUseLicenseStartEvent | 61 | whoAmI (int), NPC type (float as number2) | C->S | Server checks NPCID.Sets.MPAllowedEnemies[type] and !NPC.AnyNPCs(type) | Yes (with gate) | Summon any boss with MPAllowedEnemies set; negative number2 values trigger moon events |
| RequestNPCBuff | 53 | NPC index (short), buff type (short) | C->S | Server applies buff to NPC without proximity check | Yes | Apply any buff to any NPC |
| StrikeNPC | 28 | NPC index (short), damage (short), knockback (float), hit direction (byte), crit flag (byte) | C->S->C | Server validates NPC index in range and active; damage is forwarded without cap | Yes | One-shot any NPC with large damage value |
| SyncNPC | 23 | NPC index (short), NPC state fields | S->C | Server-authoritative | No | Client sending this is ignored |
| KillProjectile | 29 | projectile index (short), whoAmI (byte) | C->S->C | Server validates whoAmI matches projectile owner | Yes (own projectiles) | Kill own projectiles only |
| SyncProjectile | 27 | projectile index (short), position (Vector2), velocity (Vector2), owner (byte), type (short), AI fields | C->S->C | Server forwards if owner matches sender slot | Yes (own projectiles) | Teleport own projectiles |
| TogglePVP | 30 | whoAmI (byte), PvP state (bool) | C->S->C | Client-authoritative for own slot | Yes | Toggle own PvP flag |
| PlayerPvPTeam | 45 | whoAmI (byte), team index (byte) | C->S->C | Client-authoritative for own slot | Yes | Switch own team freely |
| TogglePartyFlag | 62 | party flag type (byte) | C->S->C | Server validates type index | Partial | Toggle party events if server permits |
| WorldData | 7 | Full world state blob (time, flags, hardmode, downed bosses, weather) | S->C only | Server-authoritative | No | Server ignores this from clients |
| SpawnTileData | 49 | tileX (short), tileY (short) | C->S | Client requests tile data for a region | Yes | No direct abuse vector |
| StartPlayingTrackedMusic | 101 | music ID (int) | S->C | Server-authoritative | No | Server ignores from clients |
| CaughtNPC | 70 | item index (short), NPC type (short) | C->S | Server validates matching context | Partial | Claim a caught critter; requires matching context |
| Teleport | 65 | teleport type (byte), source index (int), pos.X (float), pos.Y (float), style (int) | C->S->C | Server validates type and source ownership; style 3 appears unrestricted | Yes (style 3) | Teleport self to any coordinate; confirmed in CheatSheet QuickTeleportHotbar |

Notes:
- Value 61 is used in CheatSheet EventManagerHotbar for pumpkin moon (number2=-4f) and frost moon (number2=-5f) as variants of SpawnBossUseLicenseStartEvent.
- SyncItem and ItemDrop share integer value 21; tModLoader exposes both names mapping to it.
- SendStrikeNPC is a tModLoader wrapper that constructs and sends packet 28 (StrikeNPC).

---

## Section 2: Feature to MessageID Mapping

docs/analysis/cheatsheet_features.md does not exist at this time. The following is inferred from CheatSheet source (references/CheatSheet/) and ExampleMod patterns. TODO: cross-check once that file is produced.

| Feature | Classification | MessageID(s) | Required fields | Notes |
|---|---|---|---|---|
| Item spawn (give self item) | REIMPLEMENT_CLIENTSIDE | SyncEquipment (5) | whoAmI=Main.myPlayer; slot=inventory index; netID=item ID; stack=count; prefix=modifier byte | Targets inventory slot directly |
| Item spawn on ground | REIMPLEMENT_CLIENTSIDE | SyncItem/ItemDrop (21) | item index; position=player position; netID; stack; prefix | CheatSheet RecipeQuerySlot and VacuumItems confirm this path |
| NPC spawn (MPAllowedEnemies) | REIMPLEMENT_CLIENTSIDE | SpawnBossUseLicenseStartEvent (61) | number=player.whoAmI; number2=(float)NPC type | Only works if NPCID.Sets.MPAllowedEnemies[type] is true |
| NPC spawn (non-allowed types) | WORKAROUND_REQUIRED | none | -- | Must run server-side; no client packet triggers arbitrary NPC spawning on a vanilla server |
| Apply buff to self | REIMPLEMENT_CLIENTSIDE | AddPlayerBuff (55) | whoAmI=Main.myPlayer; buffType=buff ID; time=duration ticks | Server applies and rebroadcasts |
| Remove debuffs from self | REIMPLEMENT_CLIENTSIDE | PlayerBuffs (50) | whoAmI=Main.myPlayer; 22-element array with zeros in cleared slots | Server forwards the array |
| God mode (infinite HP) | REIMPLEMENT_CLIENTSIDE | PlayerLife (16) | whoAmI; statLife=desired value; statLifeMax=same | Resend each tick to counter incoming damage; server forwards |
| Teleport to coordinate | REIMPLEMENT_CLIENTSIDE | Teleport (65) | number=0; number2=(float)player.whoAmI; number3=pos.X; number4=pos.Y; number5=3 | Confirmed: CheatSheet QuickTeleportHotbar.RunTeleport and HandleRandomTeleport both send value 65 with these arguments |
| PvP toggle | REIMPLEMENT_CLIENTSIDE | TogglePVP (30) | whoAmI=Main.myPlayer; pvp bool | Client-authoritative |
| Team switch | REIMPLEMENT_CLIENTSIDE | PlayerPvPTeam (45) | whoAmI=Main.myPlayer; team index | Client-authoritative |
| Clear dropped items | REIMPLEMENT_CLIENTSIDE | SyncItem (21) | item index for each active item; stack=0 to clear | CheatSheet HandleClearItems: NetMessage.SendData(21, -1, -1, null, i, 0f, 0f, 0f, 0) |
| Clear own projectiles | REIMPLEMENT_CLIENTSIDE | KillProjectile (29) | projectile index; whoAmI=Main.myPlayer | Can only kill own projectiles; server rejects mismatched owner |
| NPC butcher | REIMPLEMENT_CLIENTSIDE | StrikeNPC (28) via SendStrikeNPC | NPC index; damage=npc.lifeMax; direction; crit | CheatSheet NPCButchererHotbar and CheatSheetNPC.PreAI use SendStrikeNPC |
| Tile edit (single) | REIMPLEMENT_CLIENTSIDE | TileManipulation (17) | action; tileX; tileY; type | Reach validation is lenient in vanilla |
| Tile edit (mass region) | REIMPLEMENT_CLIENTSIDE | TileSquareSync (20) | tileX; tileY; size; tile data blob | No reach restriction on size in vanilla |
| Spawn pumpkin moon | REIMPLEMENT_CLIENTSIDE | SpawnBossUseLicenseStartEvent (61) | number=player.whoAmI; number2=-4f | CheatSheet EventManagerHotbar bPumpkinMoon_onLeftClick |
| Spawn frost moon | REIMPLEMENT_CLIENTSIDE | SpawnBossUseLicenseStartEvent (61) | number=player.whoAmI; number2=-5f | CheatSheet EventManagerHotbar bFrostMoon_onLeftClick |
| Set world time | WORKAROUND_REQUIRED | none | -- | WorldData (7) is server->client only; client writes to Main.time are overwritten by the next WorldData packet |
| Set weather | WORKAROUND_REQUIRED | none | -- | Rain/weather fields are inside WorldData; no client-to-server weather packet exists |
| Set downed boss flags | WORKAROUND_REQUIRED | none | -- | NPC.downedBoss* flags live in server state; WorldData flows only server->client |
| Toggle hardmode | WORKAROUND_REQUIRED | none | -- | Main.hardMode is server-authoritative; no client packet modifies it |
| Start invasion (goblin/pirate/martian) | WORKAROUND_REQUIRED | none directly | -- | Main.StartInvasion must run on the server; CheatSheet guards these with if (Main.netMode != 1) |
| Start slime rain | WORKAROUND_REQUIRED | none | -- | Main.StartSlimeRain is server-only |
| Start solar eclipse | WORKAROUND_REQUIRED | none | -- | CheatSheet bSolarEclipse_onLeftClick guards with Main.netMode != 1; sends WorldData (7) from server |
| Start blood moon | WORKAROUND_REQUIRED | none | -- | Main.bloodMoon is server state; WorldData is the only propagation mechanism |
| Vacuum items to player | REIMPLEMENT_CLIENTSIDE | SyncItem (21) | For each active item, update position to player coords | CheatSheet Hotbar.VacuumItems performs exactly this with value 21 |

---

## Section 3: Server-Rejection Flags

| Feature | Rejection reason |
|---|---|
| Set Main.hardMode | WorldData (7) flows exclusively server->client; the server ignores this packet from a client |
| Set downed boss flags | All flags are serialized inside WorldData; updated only when server broadcasts new world state |
| Set world time | Time is advanced server-side each tick and broadcast via WorldData; client writes are overwritten |
| Control rain / weather | These fields are inside the WorldData payload; no dedicated client-to-server weather packet exists |
| Stop moon events | Main.stopMoonEvent() and moon event flags are server-side state; no client packet halts a running event |
| Start invasions | Main.StartInvasion must run on the server; no client-to-server invasion-start packet exists in vanilla |
| Start slime rain | Main.StartSlimeRain is server-only; client calling it locally has no network effect |
| Start solar eclipse | Eclipse state is in WorldData; NetMessage.SendData(7) is only valid from the server |
| Start blood moon | Main.bloodMoon is server state; WorldData is the only propagation mechanism |
| Spawn arbitrary NPCs (non-MPAllowedEnemies) | NPC.NewNPC must execute on the server; SpawnBossUseLicenseStartEvent (61) drops the packet for types not in NPCID.Sets.MPAllowedEnemies |
| Kill NPC via SyncNPC | SyncNPC (23) is server->client; the server ignores it from clients; StrikeNPC (28) is the only viable client kill vector |
| Assign item ownership to another player | ItemOwner (22) is server->client; the server assigns ownership; a client sending this packet is ignored |

---

## Section 4: Open Questions

- Whether modern Terraria (1.4.4+) SpawnBossUseLicenseStartEvent (61) with a positive NPC type value checks that the sending client inventory contains the corresponding summon item, or only checks NPCID.Sets.MPAllowedEnemies. The ExampleMod comment in MinionBossSummonItem.cs describes only the !NPC.AnyNPCs(id) check; item possession check needs live testing.

- Whether StrikeNPC (28) on a vanilla server enforces a maximum damage cap or proximity requirement. CheatSheet sends npc.lifeMax as damage without clamping; needs confirmation that vanilla servers relay this verbatim.

- Whether the server enforces that the whoAmI field in AddPlayerBuff (55) must match the sender slot only, or accepts applications to arbitrary player indices (which would allow forced-buffing other players).

- Whether TileSquareSync (20) enforces a maximum patch size server-side. QTRHacker WorldPainter implies large blobs work; the extent of server-side size validation needs empirical confirmation.

- Whether Teleport (65) style 3 on a modern vanilla server requires the player to have the Rod of Discord in inventory, or whether the server forwards any teleport request with style 3 unconditionally. CheatSheet uses this without an item check, but that code ran under a server-relayed ModPacket path; the pure client->server path may differ.

- Whether PlayerBuffs (50) sent from a client with a whoAmI value equal to another player index is forwarded by the server, enabling forced debuff manipulation on other players.

- The exact integer value of SpawnBossUseLicenseStartEvent in the current tModLoader MessageID enum. Reference code uses the named constant; the underlying integer should be confirmed to remain 61 in the active tModLoader build targeted by this project.

