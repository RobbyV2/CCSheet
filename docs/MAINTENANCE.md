# Maintenance Guide

This document captures the invariants that future edits, especially upstream merges, must respect. Read it before accepting any patch that touches serialization, packet handling, or identifier strings.

## Upstream sync workflow

The upstream Cheat Sheet lives as a submodule at `references/CheatSheet`. To pull incoming changes and diff them against this fork:

```
git -C references/CheatSheet fetch --all
git -C references/CheatSheet log origin/1.4.4..HEAD --oneline
```

For a file-by-file diff against the fork source:

```
git diff references/CheatSheet/Menus/Slot.cs Menus/Slot.cs
```

Apply relevant changes by hand rather than wholesale copy. Upstream is server-synced; this fork is client-sided, so packet code and server-authoritative logic must be translated through the stub map in the "Stubbed ModPacket sites" section or rewired to vanilla `NetMessage.SendData`. Commit the submodule pointer bump separately from mod-source edits so the "what upstream changed" and "what I accepted" diffs stay legible.

## Identifier sweep invariants

When renaming identifiers across the tree, the following strings must be preserved exactly. They reference upstream file paths and server endpoints that outlive the fork's internal name.

Save file paths (upstream compat):

- `CheatSheetConfig.json`
- `CheatSheetConfig_Server.json`

Local asset paths:

- `CheatSheet_PaintTools.txt`
- `CheatSheet_PaintTools_{index++}.json`

Upstream server URLs:

- `CheatSheet_Schematics_GetList.php`
- `CheatSheet_Schematics_Submit.php`
- `CheatSheet_Schematics_Vote.php`

Metadata:

- `homepage` field in `build.txt` pointing at the upstream repo.

A sed sweep of `s/ClientCheats/CCSheet/g` or `s/CCSheet/<NewName>/g` is safe against these because none contain those identifiers. Any future rename pass that would affect `CheatSheet` as a substring must be scoped more carefully.

## Newtonsoft.Json prohibition

This mod uses `System.Text.Json` exclusively. Any upstream patch that reintroduces `Newtonsoft.Json` must be converted before the patch is accepted. Conversion rules:

| Newtonsoft | STJ equivalent |
|---|---|
| `JsonConvert.SerializeObject(x)` | `JsonSerializer.Serialize(x, options)` |
| `JsonConvert.DeserializeObject<T>(s)` | `JsonSerializer.Deserialize<T>(s, options)` |
| `[JsonProperty("name")]` | `[JsonPropertyName("name")]` |
| `[JsonIgnore]` (on non-ModConfig) | `[JsonIgnore]` from `System.Text.Json.Serialization` |
| `JObject` / `JToken` | `JsonElement` / `JsonNode` |

### tML ModConfig exception

tML's `ConfigManager` uses Newtonsoft exclusively to serialize `ModConfig` subclasses. If a `ModConfig` subclass ever needs `[JsonIgnore]`, that attribute must be the Newtonsoft-namespaced `Newtonsoft.Json.JsonIgnoreAttribute`, because the STJ attribute is invisible to tML's serializer. At the time of this writing no `ModConfig` in this fork uses `[JsonIgnore]`, so no Newtonsoft reference is needed. If one becomes necessary, add a using-alias (`using NewtonsoftJsonIgnore = Newtonsoft.Json.JsonIgnoreAttribute;`) and apply it only on the ModConfig subclass, leaving all other serialization on STJ.

## Stubbed ModPacket sites

Seven locations carry `TODO(clientcheats): rewire to NetMessage.SendData` markers. Until rewired, the associated multiplayer features are non-functional:

| File | Method | Feature |
|---|---|---|
| `Menus/Hotbar.cs` | `SyncVacuum` | Vacuum pull |
| `Menus/NPCSlot.cs` | `SyncNPC`, `onRightClick` FilterNPC | NPC spawn and filter |
| `Menus/QuickTeleportHotbar.cs` | `SyncTeleport` | Quick teleport |
| `Menus/NPCButchererHotbar.cs` | `SyncButcher` | NPC butcher |
| `Menus/QuickClearHotbar.cs` | `SyncQuickClear` | Quick clear |
| `Menus/SpawnRateMultiplier.cs` | `RequestSetSpawnRate`, `HandleSetSpawnRate` | Spawn rate |
| `CCSheet.cs` | `HandlePacket` | Server relay stub |

The rewire plan lives in `docs/analysis/strategy.md`. Each site needs a vanilla-server-compatible path: either reuse an existing `MessageID` packet, or drop the feature in multiplayer and gate it with a `Main.netMode` check.

## tML ModCompile quirk

tML's in-game Build+Reload does not invoke MSBuild. It uses Roslyn directly through `ModCompile.GetTerrariaReferences` (`references/tModLoader/patches/tModLoader/Terraria/ModLoader/Core/ModCompile.cs`, around lines 505 to 519). Consequence: MSBuild `<Target>` hooks in `CCSheet.csproj` fire only during `dotnet build`, never during Build+Reload. Any logic that must run on both paths has to live in the source itself or in a pre-build script the user runs by hand.

## Version gate

`CCSheet.cs` around line 77 holds `BuildInfo.tMLVersion < new Version(2026, 2, 3)`. Raise this floor when adopting APIs introduced in newer tML releases. Do not raise it gratuitously; the floor is a user-facing compat promise.
