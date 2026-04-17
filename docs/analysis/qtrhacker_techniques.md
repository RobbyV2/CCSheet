# QTRHacker Techniques: Analysis for ClientCheats

Generated: 2026-04-16

## Architecture Overview

QTRHacker is a **fully external** cheat tool. It runs in a separate process and operates on a live Terraria process via:

1. **QHackLib** -- a custom CLR debugger/memory layer built on the .NET DAC (mscordaccore). It reads and writes arbitrary .NET object fields by traversing the CLR heap via the IXCLRDataProcess COM interface. Every field access crosses a VirtualReadEx/VirtualWriteEx syscall boundary.
2. **QHackCLR** -- a native C++ module that hosts the DAC, enumerates CLR types, and provides ReadProcessMemory/WriteProcessMemory primitives.
3. **Inline-patching** -- for toggle-style cheats, QTRHacker performs AOB scans in the target process and overwrites instructions (JMP trampolines, NOP sleds, single-byte ADD/SUB flips) using WriteProcessMemory.
4. **RemoteThread execution** -- assembles x86 shellcode, allocates executable memory in the target process, and spins a new CLR managed thread to execute it from within the game loop (hooked onto Terraria.Main.Update).

**None of these cross-process mechanisms are available or necessary inside a tModLoader mod.** A mod already runs in-process in the same AppDomain as Terraria. Direct field assignment, Player.AddBuff(), NPC.NewNPC(), and NetMessage.SendData() replace every technique QTRHacker implements via remote writes.

The QTRHacker.Patches project is the only component that uses Harmony and runs in-process (injected by the launcher). Its logic is architecturally identical to what a tModLoader mod can do natively with ModSystem.PostUpdateEverything or a PlayerUpdate hook.

---
## Section 1: Summary Table

| Technique | QTRHacker source file | Classification | GPL_RISK | Proposed mapping | Notes |
|---|---|---|---|---|---|
| Infinite Life | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.statLife = Player.statLifeMax each tick | QTRHacker patches code; mod assigns field directly |
| Infinite Mana | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.statMana = Player.statManaMax each tick | Direct field assignment |
| Infinite Oxygen | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.breath = Player.breathMax each tick | Player.breath, Player.breathMax |
| Infinite Fly Time | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.wingTime = Player.wingTimeMax each tick | Player.wingTime |
| Infinite Minions | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.maxMinions = 9999 in ModPlayer.PostUpdateEquips | Player.maxMinions, Player.maxTurrets |
| Infinite Ammo | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Override ModPlayer.CanConsumeAmmo to return false | Item.stack on ammo items |
| Immune to Debuffs | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Set Player.buffImmune[type]=true in ModPlayer.ResetEffects | Player.buffImmune[], Main.debuff[] |
| Ghost Mode | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.ghost = true in ModPlayer.PostUpdate | Player.ghost bool |
| Full-screen Lighting | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.nightVision=true or Lighting.AddLight | Client-side only |
| Journey Mode Menu | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Player.difficulty=3; Main.CreativeMenu.Enabled=true | SP only; server rejects on MP |
| Unlock All Journey Duplications | Scripts/Functions/BuiltIn-1.cs | LOCAL_PLAYER_STATE | LOW | Loop RegisterItemSacrifice for all item IDs | Direct in-process call |
| Slow Fall | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | Player.slowFall=true in ModPlayer.PostUpdateEquips | Player.slowFall bool |
| Super Fast Speed | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | Player.moveSpeed=20f each tick | Player.moveSpeed float |
| Super Grab Range | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | Override ModPlayer.GrabRange | Overridable in tModLoader |
| Bonus Accessory Slots | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | ModPlayer.ExtraAccessorySlotCount | tModLoader native API |
| Enable All Recipes | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | Set Main.numAvailableRecipes + fill Main.availableRecipe[] in ModSystem.PostResetRecipes | Client-side only |
| Burn All NPCs | Scripts/Functions/BuiltIn-3.cs | CRAFTABLE_NETMESSAGE | LOW | NPC.AddBuff(153,216000) per active NPC; MP: NetMessage.SendData(MessageID.NPCAddBuff) | Buff 153 = Daybroken |
| Burn All Players | Scripts/Functions/BuiltIn-3.cs | CRAFTABLE_NETMESSAGE | LOW | Player.AddBuff(44,216000) per active player | Buff 44 = On Fire |
| Reveal Whole Map | Scripts/Functions/BuiltIn-3.cs | LOCAL_PLAYER_STATE | LOW | Main.Map.UpdateLighting(x,y,255) loop + Main.refreshMap=true | Purely client-side |
| Right-click Map Teleport | Scripts/Functions/BuiltIn-3.cs | LOCAL_PLAYER_STATE | LOW | Unproject mouse from map space; set Player.position | MessageID.TeleportationPotion for MP sync |
| Randomize UUID | Scripts/Functions/BuiltIn-3.cs | LOCAL_PLAYER_STATE | LOW | Main.clientUUID = Guid.NewGuid().ToString() | One-shot |
| Toggle Day/Night | Scripts/Functions/BuiltIn-4.cs | CRAFTABLE_NETMESSAGE | LOW | Main.dayTime=!Main.dayTime + NetMessage.SendData(MessageID.WorldData) | MessageID.WorldData=7 |
| Toggle Sundial | Scripts/Functions/BuiltIn-4.cs | CRAFTABLE_NETMESSAGE | LOW | Main.fastForwardTime=!Main.fastForwardTime | Server-side state in MP |
| Toggle Blood Moon | Scripts/Functions/BuiltIn-4.cs | CRAFTABLE_NETMESSAGE | LOW | Main.bloodMoon=!Main.bloodMoon + broadcast | Main.bloodMoon bool |
| Toggle Eclipse | Scripts/Functions/BuiltIn-4.cs | CRAFTABLE_NETMESSAGE | LOW | Main.eclipse=!Main.eclipse + broadcast | Main.eclipse bool |
| Toggle Frost Moon | Scripts/Functions/BuiltIn-4.cs | CRAFTABLE_NETMESSAGE | LOW | Main.snowMoon=!Main.snowMoon + broadcast | Main.snowMoon bool |
| Toggle Pumpkin Moon | Scripts/Functions/BuiltIn-4.cs | CRAFTABLE_NETMESSAGE | LOW | Main.pumpkinMoon=!Main.pumpkinMoon + broadcast | Main.pumpkinMoon bool |
| Super Interaction Range | Scripts/Functions/BuiltIn-5.cs | LOCAL_PLAYER_STATE | LOW | Player.tileRangeX=4096; Player.tileRangeY=4096 each tick | Server may enforce range limits |
| Fast Tile/Wall Placement | Scripts/Functions/BuiltIn-5.cs | LOCAL_PLAYER_STATE | LOW | Player.tileSpeed=20f; Player.wallSpeed=20f each tick | Direct field assignment |
| Mechanical Ruler | Scripts/Functions/BuiltIn-5.cs | LOCAL_PLAYER_STATE | LOW | Player.rulerGrid=true each tick | Visual only |
| Mechanical Lens | Scripts/Functions/BuiltIn-5.cs | LOCAL_PLAYER_STATE | LOW | Player.InfoAccMechShowWires=true each tick | Visual only |
| Coin Portal Drops Bags | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | GlobalProjectile.AI hook for projectile type 518 | Projectile-specific |
| Fish Crates Only | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | GlobalProjectile hook on fishing catch resolution | Clean-room trivial |
| Strengthen Vampire Knives | Scripts/Functions/BuiltIn-2.cs | LOCAL_PLAYER_STATE | LOW | GlobalProjectile.AI for knife type; change spread angle constant | Low general utility |
| NPC Spawn | GameObjects/Terraria/NPC.cs | CRAFTABLE_NETMESSAGE | LOW | NPC.NewNPC(source, x, y, type) in-process | Direct static call |
| Item Spawn | GameObjects/Terraria/Item.cs | CRAFTABLE_NETMESSAGE | LOW | Item.NewItem(source, x, y, w, h, type, stack) in-process | MessageID.ItemDrop for MP |
| Player Buff and Pet and Mount Grant | ViewModels/PagePanels/PlayersPageViewModel.cs | LOCAL_PLAYER_STATE | LOW | Player.AddBuff(type, time) | Direct method call |
| Player Properties Editor | ViewModels/PlayerEditor/PlayerPropertiesEditorViewModel.cs | LOCAL_PLAYER_STATE | LOW | Direct field writes: statLifeMax, statManaMax, color fields | All public Terraria fields |
| Inventory and Armor Editor | ViewModels/PlayerEditor/ItemSlotsEditorViewModel.cs | LOCAL_PLAYER_STATE | LOW | Item.SetDefaults(type) + item.prefix + item.stack | MessageID.SyncEquipment for MP |
| Inventory Save and Load | GameObjects/Terraria/Player.cs | LOCAL_PLAYER_STATE | LOW | Binary serialize (type, stack, prefix) per slot | Format is independently designable |
| Teleport to Player | ViewModels/PagePanels/PlayersPageViewModel.cs | LOCAL_PLAYER_STATE | LOW | Main.LocalPlayer.position = Main.player[i].position | MessageID.TeleportationPotion for MP |
| World Painter and Schematic Brush | QTRHacker.Patches/WorldPainter.cs | WORLD_EDIT | MEDIUM | Framing.GetTileSafely copy/paste; WorldGen.SquareTileFrame; NetMessage.SendTileSquare | Use Tile struct directly; avoid parallel STile struct |
| Aimbot | QTRHacker.Patches/AimBot.cs | LOCAL_PLAYER_STATE | LOW | ModSystem.PostUpdateEverything hook; quadratic intercept; set PlayerInput.MouseX/Y | Ballistic formula is standard kinematics |
| Auto Fishing Bot | QTRHacker.Patches/AutoFishing.cs | LOCAL_PLAYER_STATE | LOW | Hook ModPlayer.PreItemCheck; inspect bobber ai[1], localAI[1]; call Player.ItemCheck() | Bobber state fields publicly documented |
| External Process Memory Layer | src/QHackLib/ src/QHackCLR/ | PROCESS_EXTERNAL | HIGH | Not applicable | Entire cross-process stack; irrelevant in-process |
| Inline Hook and Remote Thread Engine | src/QHackLib/FunctionHelper/ | PROCESS_EXTERNAL | HIGH | Not applicable | WriteProcessMemory-based; Harmony replaces in-process |
| Projectile Image and DSL | src/QTRHacker.Core/ProjectileImage/ ProjectileMaker/ | LOCAL_PLAYER_STATE | MEDIUM | Custom projectile scripting; design DSL from scratch | DSL AST structure is novel; clean-room redesign required |

---

## Section 2: Per-Technique Detail

### Infinite Life

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: QTRHacker AOB-scans for the machine instruction that subtracts incoming damage from Player.statLife and replaces the SUB opcode with ADD via WriteProcessMemory, converting damage taken into healing. When disabled, restores the original byte. The in-process equivalent is assigning player.statLife = player.statLifeMax once per game tick in ModPlayer.PostUpdate.
- Target Terraria fields: Player.statLife (int), Player.statLifeMax (int)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: The stat is broadcast automatically via vanilla net sync (MessageID.PlayerControls, type 13) so the server sees the corrected value. Continuous damage sources (lava, burning debuffs) will transiently reduce HP between ticks; the player never dies but the value flickers by one tick.

---

### Infinite Mana

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Two AOB scans locate both instructions of the form SUB [esi+manaOffset], reg that drain mana on spell cast and replace each SUB with ADD so casting increases mana. In-process equivalent is assigning player.statMana = player.statManaMax each tick.
- Target Terraria fields: Player.statMana (int), Player.statManaMax (int)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Mana is broadcast in MessageID.PlayerControls; no desync concern. Spell-cast eligibility checks read statMana before the cast, so restoring it each tick before that check is sufficient.

---

### Infinite Oxygen

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Locates the DEC dword ptr [eax+breath] instruction that counts down the underwater breath timer and replaces it with INC, preventing the timer from reaching zero. In-process equivalent: player.breath = player.breathMax each update.
- Target Terraria fields: Player.breath (int), Player.breathMax (int)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Breath state is client-local; the server does not track or enforce it.

---

### Infinite Fly Time

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Locates the floating-point store that writes the decremented wing timer back to Player.wingTime and NOPs it, preventing decrement. In-process: player.wingTime = player.wingTimeMax (or a large constant) each tick.
- Target Terraria fields: Player.wingTime (float), Player.wingTimeMax (float), Player.empressBrooch (bool, used as AOB disambiguation anchor)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: wingTime is not directly synced to the server. The server observes flight from position updates only.

---

### Infinite Minions

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Locates the initialization instructions that write Player.maxMinions = 1 and Player.maxTurrets = 1 at the start of each update cycle and patches the immediate values to 9999. In-process: assign both fields to 9999 in ModPlayer.PostUpdateEquips (after vanilla accessories have run).
- Target Terraria fields: Player.maxMinions (int), Player.maxTurrets (int)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Both fields are reset in Player.ResetEffects each tick before accessories apply; the mod hook must run after that phase.

---

### Infinite Ammo

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Two AOB patterns locate the two DEC dword ptr [reg+0xB0] instructions that decrement ammo Item.stack on each projectile fired. Both are NOPed. In-process equivalent: override ModPlayer.CanConsumeAmmo(Item weapon, Item ammo) to return false, the tModLoader-sanctioned hook for this purpose.
- Target Terraria fields: Item.stack (int) on the ammo item in Player.inventory
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: CanConsumeAmmo is a dedicated tModLoader hook; no assembly analysis required.

---

### Immune to Debuffs

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Installs an inline hook at the entry of Player.AddBuff. The injected shellcode checks Main.debuff[buffType]; if true (the buff being added is a debuff), it executes an early return before the buff is applied. In-process equivalent: iterate player.buffType[] in ModPlayer.PostUpdateBuffs and remove entries where Main.debuff[type] is true, or pre-populate player.buffImmune[] for all debuff IDs in ModPlayer.ResetEffects.
- Target Terraria fields: Main.debuff[] (bool array), Player.buffImmune[] (bool array), Player.buffType[], Player.buffTime[]
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: The cleanest in-process approach sets player.buffImmune[type] = true for every debuff type before Player.UpdateBuffs runs.

---

### Ghost Mode

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Writes Player.ghost = true via cross-process memory write. In-process: single field assignment.
- Target Terraria fields: Player.ghost (bool)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Ghost disables tile collision and grants noclip. Some server anti-cheats flag the ghost flag for non-creative-mode players.

---

### Full-screen Lighting

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Hooks the lighting update routine and forces all per-tile RGB lighting coefficients to 1.0f on every frame. In-process: set player.nightVision = true and player.findTreasure = true for a softer effect, or call Lighting.AddLight across a wide area for stronger coverage.
- Target Terraria fields: Internal lighting float arrays; Player.nightVision (bool), Player.findTreasure (bool)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Lighting is fully client-side. No server interaction or desync risk.

---

### Journey Mode Menu Force

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Patches the conditional jump that guards the Creative menu display check based on Player.difficulty == 3, converting it to an unconditional jump. In-process: assign Player.difficulty = 3 and set Main.CreativeMenu.Enabled = true.
- Target Terraria fields: Player.difficulty (byte), Main.CreativeMenu Enabled backing field (bool)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Vanilla multiplayer servers will refuse creative menu actions if the world is not in journey mode. Singleplayer and journey-mode servers are unaffected.

---

### Unlock All Journey Duplications

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-1.cs
- Mechanism: Allocates a shellcode loop on a managed thread that iterates all item IDs (0 to MaxItemTypes) and calls ItemsSacrificedUnlocksTracker.RegisterItemSacrifice(tracker, id, 9999) for each. In-process: a simple C# for-loop calling the same method directly.
- Target Terraria fields: Main.CreativeMenu.creativeTracker.ItemSacrifices (the tracker instance)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Client-side state. The server does not validate duplication unlock state.

---

### Slow Fall

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-2.cs
- Mechanism: Hooks the instruction that writes Player.slowFall during the per-tick equipment reset and forces the value to 1. In-process: player.slowFall = true in ModPlayer.PostUpdateEquips.
- Target Terraria fields: Player.slowFall (bool)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: No server sync needed; fall speed is computed client-side.

---

### Super Fast Speed

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-2.cs
- Mechanism: Hooks the float store for Player.moveSpeed and writes the IEEE-754 representation of 20.0f (0x41A00000). In-process: player.moveSpeed = 20f in ModPlayer.PostUpdateEquips.
- Target Terraria fields: Player.moveSpeed (float)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Abnormal movement speed is observable by other players and may trigger server-side position correction on anti-speedhack servers.

---

### Super Grab Range

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-2.cs
- Mechanism: Replaces the entry of Player.GetItemGrabRange entirely with a stub returning 1000 via early RET. In-process: override ModPlayer.GrabRange (the tModLoader equivalent hook) to return a large integer.
- Target Terraria fields: Return value of Player.GetItemGrabRange()
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: No server interaction needed; item pickup range is checked client-side.

---

### Bonus Accessory Slots

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-2.cs
- Mechanism: Replaces Player.IsAValidEquipmentSlotForIteration with a stub returning true (1). In tModLoader, set ModPlayer.ExtraAccessorySlotCount to extend accessory slots natively up to 2, or hook slot iteration for more.
- Target Terraria fields: Player.IsAValidEquipmentSlotForIteration() return value
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: The server enforces equipment slot counts only for specific item effects; visual slot expansion is client-side.

---

### Enable All Recipes

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-2.cs
- Mechanism: Writes 0xC3 (RET) at Recipe.FindRecipes entry to disable recipe filtering, then directly writes Main.numAvailableRecipes = 3000 and fills Main.availableRecipe[0..2999] with sequential indices. In-process: hook ModSystem.PostResetRecipes and perform the same assignments.
- Target Terraria fields: Main.numAvailableRecipes (int), Main.availableRecipe[] (int array)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Client-side display only. The server does not validate recipe eligibility during standard crafting.

---

### Burn All NPCs

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-3.cs
- Mechanism: Iterates Main.npc[] and calls NPC.AddBuff(153, 216000) (Daybroken, roughly one hour) on each active entry via remote managed thread invocation. In-process this is a direct loop calling npc.AddBuff. For multiplayer: NetMessage.SendData(MessageID.NPCAddBuff, npcIndex, buffType, buffTime).
- Target Terraria fields: Main.npc[], NPC.active, NPC.buffType[], NPC.buffTime[]
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: In multiplayer, only server-side mods can authoritatively apply buffs to NPCs. Client-sent NPCAddBuff packets may be dropped.

---

### Burn All Players

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-3.cs
- Mechanism: Same loop pattern targeting Main.player[] with buff type 44 (On Fire, 216000 ticks).
- Target Terraria fields: Main.player[], Player.active, Player.buffType[], Player.buffTime[]
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Applying buffs to remote players from a client is generally rejected by vanilla servers.

---

### Reveal Whole Map

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-3.cs
- Mechanism: Executes a nested loop over all tile coordinates calling WorldMap.UpdateLighting(x, y, 255) on a managed thread, then sets Main.refreshMap = true to trigger a redraw. In-process: same loop calling Main.Map.UpdateLighting directly.
- Target Terraria fields: Main.Map (WorldMap instance), Main.refreshMap (bool), Main.maxTilesX (int), Main.maxTilesY (int)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Purely client-side map rendering state. The loop is expensive on large worlds; consider spreading it over multiple ticks.

---

### Right-click Map Teleport

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-3.cs
- Mechanism: Hooks Main.Update and on each tick checks whether the fullscreen map is open and the right mouse button was just released. If so, converts mouse screen coordinates to world coordinates using Main.mapFullscreenScale, Main.mapFullscreenPos, Main.screenWidth, Main.screenHeight, and tile size (16 pixels), then writes the result to LocalPlayer.position. The conversion is linear unprojection.
- Target Terraria fields: Main.mapFullscreen (bool), Main.mouseRight (bool), Main.mouseRightRelease (bool), Main.mapFullscreenScale (float), Main.mapFullscreenPos (Vector2), Entity.position (Vector2) on LocalPlayer
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Standard linear map-to-world projection; not copyrightable math. For multiplayer server acknowledgment, send MessageID.TeleportationPotion (type 96) after the position update.

---

### Randomize UUID

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-3.cs
- Mechanism: Overwrites Main.clientUUID string with a new GUID. In-process: Main.clientUUID = Guid.NewGuid().ToString().
- Target Terraria fields: Main.clientUUID (string)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: UUID is sent at connection handshake. Mid-session change has no immediate effect but could confuse server logging.

---

### Toggle Day/Night

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-4.cs
- Mechanism: Flips Main.dayTime. For multiplayer propagation: NetMessage.SendData(MessageID.WorldData) (type 7).
- Target Terraria fields: Main.dayTime (bool)
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: In singleplayer this is immediate. In multiplayer only the server sends WorldData with authority.

---

### Toggle Sundial

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-4.cs
- Mechanism: Flips Main.fastForwardTime, which causes the vanilla time loop to advance at accelerated rate each tick.
- Target Terraria fields: Main.fastForwardTime (bool)
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Server-side state in multiplayer; client modification will be overwritten by next WorldData sync.

---

### Toggle Blood Moon / Eclipse / Frost Moon / Pumpkin Moon

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-4.cs
- Mechanism: Flips the respective static boolean on Main. Each follows the same pattern; multiplayer broadcast requires NetMessage.SendData(MessageID.WorldData).
- Target Terraria fields: Main.bloodMoon, Main.eclipse, Main.snowMoon, Main.pumpkinMoon (all bool)
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: All four event flags are broadcast in the WorldData packet. Server-side mods have authority in multiplayer.

---

### Super Interaction Range

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-5.cs
- Mechanism: AOB-locates the two int-write instructions that set tileRangeX = 5 and tileRangeY = 4 each tick and patches the immediate values to 0x1000 (4096). In-process: player.tileRangeX = 4096; player.tileRangeY = 4096 each tick.
- Target Terraria fields: Player.tileRangeX (int), Player.tileRangeY (int)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Some servers enforce interaction range server-side and will silently reject tile actions from unusual distances.

---

### Fast Tile and Wall Placement

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-5.cs
- Mechanism: Hooks the float stores for Player.wallSpeed and Player.tileSpeed and writes the IEEE-754 constant for 10.0f (0x41200000). In-process: direct field assignment each tick.
- Target Terraria fields: Player.wallSpeed (float), Player.tileSpeed (float)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: No server enforcement; placement speed is checked client-side only.

---

### Mechanical Ruler

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-5.cs
- Mechanism: Hooks the bool write for Player.rulerGrid in the equipment reset phase and forces it to 1. In-process: player.rulerGrid = true each tick.
- Target Terraria fields: Player.rulerGrid (bool), Player.rulerLine (bool)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Visual-only client flag. No server interaction.

---

### Mechanical Lens

- Source file(s): src/QTRHacker/Scripts/Functions/BuiltIn-5.cs
- Mechanism: Same pattern, forcing Player.InfoAccMechShowWires = true. In-process: direct field assignment.
- Target Terraria fields: Player.InfoAccMechShowWires (bool)
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Visual-only client flag. No server interaction.

---

### NPC Spawn

- Source file(s): src/QTRHacker.Core/GameObjects/Terraria/NPC.cs
- Mechanism: Invokes the static method NPC.NewNPC(IEntitySource, x, y, type, start, ai0..ai3, target) via a remote managed thread shellcode. In-process this is a direct static method call available to any tModLoader mod.
- Target Terraria fields: Main.npc[], NPC.type, NPC.position, NPC.active, NPC.ai[]
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: For bosses in multiplayer, NetMessage.SendData(MessageID.SpawnBossUseTilesFraming, ...) is the correct approach for server-side NPC creation authority.

---

### Item Spawn

- Source file(s): src/QTRHacker.Core/GameObjects/Terraria/Item.cs
- Mechanism: Invokes Item.NewItem(IEntitySource, x, y, w, h, type, stack, noBroadcast, prefix, noGrabDelay) via remote managed thread. In-process: direct static call. Multiplayer: NetMessage.SendData(MessageID.ItemDrop, ...) propagates the new item.
- Target Terraria fields: Main.item[], Item.type, Item.stack, Item.prefix, Item.position, Item.active
- Classification: CRAFTABLE_NETMESSAGE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Direct call; no external tricks needed. The IEntitySource parameter accepts an EntitySource_Misc or similar.

---

### Player Buff, Pet, and Mount Grant

- Source file(s): src/QTRHacker/ViewModels/PagePanels/PlayersPageViewModel.cs
- Mechanism: Calls Player.AddBuff(type, time) via cross-process remote thread. For pets, uses the buff ID from Main.vanityPet[]. For mounts, uses Mount.mounts[index].buff. In-process these are direct calls on the player reference.
- Target Terraria fields: Player.buffType[], Player.buffTime[], Main.vanityPet[], Mount.mounts[]
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: Player.AddBuff is a public Terraria API. Pet and mount grants are identical mechanically to buff grants.

---

### Player Properties Editor

- Source file(s): src/QTRHacker/ViewModels/PlayerEditor/PlayerPropertiesEditorViewModel.cs
- Mechanism: Reads Player.statLifeMax, Player.statManaMax, and eight color fields from the cross-process player object, allows the user to modify them in the UI, and writes them back. In-process: direct property assignment.
- Target Terraria fields: Player.statLifeMax, Player.statManaMax, Player.hairColor, Player.skinColor, Player.eyeColor, Player.shirtColor, Player.underShirtColor, Player.pantsColor, Player.shoeColor
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: All fields are public in the Terraria assembly. No cross-process techniques required.

---

### Inventory and Armor Editor

- Source file(s): src/QTRHacker/ViewModels/PlayerEditor/ItemSlotsEditorViewModel.cs
- Mechanism: For each selected slot calls Item.SetDefaults(type) to load stats, then Item.Prefix(prefix) to apply a modifier, then writes Item.stack. QTRHacker provides SetDefaultsAndPrefix as a combined call.
- Target Terraria fields: Player.inventory[], Player.armor[], Player.dye[], Player.miscEquips[], Player.miscDyes[]; per Item: type, stack, prefix
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: In multiplayer, changed armor slots must be synced via NetMessage.SendData(MessageID.SyncEquipment, ...) to be visible to other players.

---

### Inventory Save and Load

- Source file(s): src/QTRHacker.Core/GameObjects/Terraria/Player.cs
- Mechanism: Serializes each item slot as (int type, int stack, byte prefix) to a binary file with a version header. Loading reconstructs via SetDefaultsAndPrefix. The format is QTRHacker-specific, not Terraria native .plr format.
- Target Terraria fields: Player.inventory[], Player.armor[], Player.dye[], Player.miscEquips[], Player.miscDyes[]
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: The schema (type/stack/prefix triplet) is derived from public Terraria Item fields and can be independently designed in any format without referencing QTRHacker binary layout.

---

### Teleport to Player

- Source file(s): src/QTRHacker/ViewModels/PagePanels/PlayersPageViewModel.cs
- Mechanism: Reads Entity.position from the selected remote player and writes it to Main.LocalPlayer.position. In-process: single field copy.
- Target Terraria fields: Entity.position (Vector2) on source player; Player.position on local player
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: For multiplayer server acknowledgment, send MessageID.TeleportationPotion (type 96) after the position update.

---

### World Painter and Schematic Brush

- Source file(s): src/QTRHacker.Patches/WorldPainter.cs
- Mechanism: Runs in-process via a Harmony prefix on Main.DoUpdate. In eye-dropper mode, user drags a selection rectangle; on release, code reads each Tile via Framing.GetTileSafely(x, y) and copies header bytes, type, wall, liquid, frameX, and frameY into an internal STile[,] clipboard. In brush mode, iterates the clipboard and writes each field back to the live Tile object, calls WorldGen.SquareTileFrame(x, y, true) to recompute tile framing, and in multiplayer calls NetMessage.SendTileSquare(-1, centerX, centerY, radius). A SpriteBatch overlay draws the selection box and brush preview.
- Target Terraria fields: Tile.type, Tile.wall, Tile.liquid, Tile.sTileHeader, Tile.bTileHeader, Tile.bTileHeader2, Tile.bTileHeader3, Tile.frameX (short), Tile.frameY (short); WorldGen.InWorld, WorldGen.SquareTileFrame, NetMessage.SendTileSquare
- Classification: WORLD_EDIT
- GPL_RISK: MEDIUM
- CheatSheet equivalent: TBD
- Notes: The STile struct in QTRHacker mirrors the Tile layout field-for-field. A clean-room implementation should use Framing.GetTileSafely and the Tile struct directly rather than introducing a parallel struct. The copy/paste tile logic and SendTileSquare call are independently derivable from the Terraria public API; the exact brush rendering and clipboard looping share structural proximity to QTRHacker, hence MEDIUM risk rather than HIGH.

---

### Aimbot

- Source file(s): src/QTRHacker.Patches/AimBot.cs
- Mechanism: Harmony prefix on Main.DoUpdate. Each tick, finds the nearest qualifying NPC or player within configurable radius. Computes the projectile lead offset using a quadratic intercept equation: given displacement d from player to target, target velocity v, and effective bullet speed s (accounting for Projectile.extraUpdates), solves (k^2-1)*s^2*t^2 - 2*|d|*k*s*cos(alpha)*t + |d|^2 = 0 where k = |v|/s, takes the positive root t, then predicts target position at t. Converts the predicted intercept to a mouse screen coordinate relative to Player.MountedCenter and sets PlayerInput.MouseX/Y.
- Target Terraria fields: Main.npc[], Main.player[], Main.LocalPlayer, Player.MountedCenter, Player.HeldItem, Item.shoot, Item.shootSpeed, Player.PickAmmo(), Projectile.extraUpdates, PlayerInput.MouseX, PlayerInput.MouseY, Entity.Center, Entity.velocity
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: The quadratic ballistic intercept formula is standard kinematics; it carries no copyright. A tModLoader implementation hooks ModSystem.PostUpdateEverything or ModPlayer.PreItemCheck. Detection risk is significant in PvP servers that log aim patterns.

---

### Auto Fishing Bot

- Source file(s): src/QTRHacker.Patches/AutoFishing.cs
- Mechanism: Harmony prefix on Main.DoUpdate. Checks each tick whether Player.HeldItem.fishingPole > 0. Searches Main.projectile[] for the active bobber (bobber==true, owner==Main.myPlayer). When bobber.ai[1] < 0 (fish on line), inspects bobber.localAI[1]: positive = item, negative = NPC/critter. Depending on the configured filter mode, triggers UseItem() which sets Player.controlUseItem=true, Player.releaseUseItem=true and calls Player.ItemCheck() to reel in. After reeling, restores the prior mouse position and re-casts automatically.
- Target Terraria fields: Player.HeldItem.fishingPole (int), Main.projectile[], Projectile.active, Projectile.owner, Projectile.bobber, Projectile.ai[], Projectile.localAI[], ItemID.Sets.IsFishingCrate[], Main.anglerQuestItemNetIDs[], Main.anglerQuest, Player.controlUseItem, Player.releaseUseItem, Player.ItemCheck()
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: LOW
- CheatSheet equivalent: TBD
- Notes: The bobber state encoding (ai[1] < 0 = bite, localAI[1] sign for item vs NPC) is publicly documented via decompiled source. A tModLoader implementation uses ModPlayer.PreItemCheck or ModSystem.PostUpdateEverything as the hook point.

---

### External Process Memory Infrastructure

- Source file(s): src/QHackLib/ (HackObject.cs, HackValue.cs, HackEntity.cs, HackMethod.cs), src/QHackCLR/ (native C++)
- Mechanism: QHackLib wraps a .NET DAC interface (IXCLRDataProcess obtained from mscordaccore.dll) to enumerate CLR heaps, resolve type metadata, and compute field offsets at runtime. HackObject is a DynamicObject subclass that intercepts property get/set and translates them into VirtualReadEx/VirtualWriteEx calls sized to the field's unmanaged type. HackValue<T> is the typed leaf wrapper; HackEntity wraps reference types. QHackCLR is a separate native module loaded by QHackLib via P/Invoke that holds the OpenProcess handle and exposes ReadMemory/WriteMemory primitives. GameContext.GetStaticFieldAddress resolves a static field's address by traversing the module->type->field chain via the DAC. None of this infrastructure is necessary in a tModLoader mod because all CLR objects are directly accessible by reference in the same AppDomain.
- Target Terraria fields: All fields accessed cross-process via HackObject proxy
- Classification: PROCESS_EXTERNAL
- GPL_RISK: HIGH
- CheatSheet equivalent: Not applicable
- Notes: This entire subsystem is the defining difference between QTRHacker's architecture and an in-process mod. No code, no structure, and no design from this layer is relevant or transferable to a tModLoader mod. Direct field references replace every cross-process accessor.

---

### Projectile Image and DSL

- Source file(s): src/QTRHacker.Core/ProjectileImage/, src/QTRHacker.Core/ProjectileMaker/
- Mechanism: QTRHacker.Core includes a scripting layer that allows defining custom projectile behavior patterns as data-driven scripts. A ProjectileImage holds a sequence of timed actions (move, rotate, spawn sub-projectile) and a ProjectileMaker evaluates them by invoking Projectile static methods via the remote managed thread mechanism. The DSL AST includes node types for vector arithmetic, conditionals, and NPC targeting queries.
- Target Terraria fields: Projectile.NewProjectile(), Projectile.position, Projectile.velocity, Projectile.ai[], Projectile.damage, Projectile.penetrate
- Classification: LOCAL_PLAYER_STATE
- GPL_RISK: MEDIUM
- CheatSheet equivalent: TBD
- Notes: The projectile creation and field manipulation targets are trivially accessible in-process. The DSL concept (scripted multi-step projectile behavior) is independently valuable and is not copyright-protectable as a concept. However, the specific AST node type hierarchy and evaluator structure share structural proximity with QTRHacker's implementation; a clean-room redesign of the DSL is required. The underlying Terraria API calls (Projectile.NewProjectile, velocity writes) carry no risk.

---

## Section 3: External-only (PROCESS_EXTERNAL) Techniques

The following components and techniques from QTRHacker are entirely predicated on external-process memory manipulation and have no analog or application inside a tModLoader mod. They are catalogued here for completeness.

- **QHackLib cross-process field access layer** (src/QHackLib/HackObject.cs, HackValue.cs, HackEntity.cs, HackMethod.cs): DLR-based dynamic proxy system that translates C# property accesses into VirtualReadEx/VirtualWriteEx calls via DAC-resolved field offsets. Entirely replaced by direct field references in-process.

- **QHackCLR native DAC host** (src/QHackCLR/): Native C++ module that loads mscordaccore.dll, acquires IXCLRDataProcess, and provides the ReadMemory/WriteMemory primitives that QHackLib depends on. Has no in-process equivalent or utility.

- **AOB scanner** (src/QHackLib/Memory/AobscanHelper.cs): Reads the target process's mapped memory regions and searches for byte patterns (with wildcard masks) to locate JIT-compiled instruction addresses. Only needed for the inline-patching subsystem; irrelevant in-process.

- **Inline hook engine** (src/QHackLib/FunctionHelper/InlineHook.cs): Writes JMP trampolines into target process memory, manages the displaced prologue bytes, and handles the original-function call chain. Replaced entirely by Harmony prefix/postfix patches or tModLoader hooks.

- **RemoteThread launcher** (src/QHackLib/FunctionHelper/RemoteThread.cs): Allocates executable memory in the target process, writes x86 shellcode, and creates a remote thread via CreateRemoteThread. The matching GameContext.RunByHookUpdate and RunOnManagedThread methods hook this thread onto Terraria.Main.Update. Replaced entirely by standard ModSystem and ModPlayer hook methods.

- **CLR and JIT helper address resolution** (src/QHackLib/CLRFunctionCaller.cs, JitHelpersManager.cs): Resolves the addresses of CLR runtime helpers (coreclr!JIT_New, clrjit entry stubs) in the target process to enable shellcode that can allocate managed objects and call managed methods cross-process. No analog exists or is needed in-process.

- **Remote assembly loader** (GameContext.LoadAssemblyAsBytes in src/QTRHacker.Core/GameContext.cs): Serializes a .NET assembly to bytes and injects it into the target process via the remote managed thread mechanism, allowing the cheat tool to load additional code into the game process at runtime. A tModLoader mod is already loaded by the game's own assembly loader; this mechanism is irrelevant.
