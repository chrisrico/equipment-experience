# Developing Equipment Experience

## Building

You need the .NET SDK (8 or later) and a Valheim install with BepInEx. The build finds Valheim in the default Steam
location on macOS, Linux and Windows; pass `-p:ValheimDir=/path/to/Valheim` otherwise.

```sh
dotnet build -c Release                     # bin/Release/EquipmentExperience.dll
dotnet build -c Release -p:Deploy=true      # also copies it into BepInEx/plugins/EquipmentExperience
dotnet build -c Release -p:Package=true     # also writes dist/EquipmentExperience-<version>.zip (Thunderstore)
                                            # and dist/EquipmentExperience-<version>/ (mod managers that import folders)
```

The game assembly is compiled against with publicized members (`BepInEx.AssemblyPublicizer.MSBuild`), so private game
fields and methods can be used directly.

## Checking a game update

```sh
dotnet run --project tools/GameApiCheck [path/to/Valheim]
```

This runs the mod's startup check for every patch group against the installed game, without launching it. For each
group it looks up the game fields, methods, patch targets and patch parameter names the group uses. It prints any that
are missing and exits with 1. A group listed as `MISSING` would be switched off in the game. Run it after every Valheim
update, before players hit the problem.

If you add a patch group, add it to `PatchGroups.All()` in `src/Plugin.cs` as well as to `Plugin.Awake`.

## Releasing

1. Update `<Version>` in `EquipmentExperience.csproj`. The plugin version and the Thunderstore manifest both read it
   from there.
2. Add an entry to `CHANGELOG.md`.
3. Build with `-p:Package=true`, test the folder in a mod manager, then upload the zip to Thunderstore.

## Testing in the game

Use a throwaway test world. Set `BaseExperience = 5` in the config so bars fill in a few hits, then turn on
`devcommands` in the console:

- `location AncientUpgradeStation SAVE` spawns a Forge of Potential in front of you. Without `SAVE`, the game turns off
  saving for that world permanently. With it, the forge stays in the world for good. Either way, don't do this in a
  world you care about.
- `spawn Upgrader4Weapon` (or `Upgrader{0-7}{Weapon|Armor}`) gives you an idol.
- Refine one item several times to see guardian stars, then extra copies.
- Upgrade an item that has experience at a workbench, and check that the experience survives.
- Fight enemies above and below the item's tier, and compare the experience gained.

## How the mod stays robust

- **The forge upgrade delegates to vanilla.** The mod only decides that the upgrade succeeds. It then answers
  vanilla's `Player.GetItemUpgradeChance` with a roll that forces that outcome, so vanilla handles messages, effects,
  stats and resources, and anything Iron Gate adds to the upgrade process still runs.
- **Patches load in groups.** Each group checks its game members, patch targets and parameter names before patching.
  If any is missing, the group is skipped and the log names what's missing. If a patch fails to apply, the group's
  other patches are undone.
- **Groups fail safe:**
  - **Enemy tier tracking fails:** every tier multiplier is 1.
  - **Workbench carry-over fails:** items only gain experience at max crafting level, so banked experience can't be
    wiped.
  - **Guardians fail:** upgrades still work, without guardians, and the forge panel stops promising them.
  - **The forge upgrade fails:** the forge panel is switched off too, and the log says the vanilla mechanic is active.
- **Errors during play are contained.** Patch bodies call their logic from inside a try block, so even a member that
  goes missing at JIT time is caught. It's logged once, and the game carries on. A failing forge upgrade is blocked
  rather than handed back to the vanilla gamble.
- **Few hard-coded game names.** Enemy tiers come from the biome an enemy stands in, or from a faction matched by name
  rather than enum value. The only prefab names are config defaults: guardians and tier overrides. A wrong name there
  is skipped with a warning.
