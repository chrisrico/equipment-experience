# Equipment Experience

A BepInEx mod for Valheim that replaces the random Forge of Potential upgrade with an experience system.

In the base game, refining an item with an idol is a gamble. Every idol succeeds 65% of the time, and every failure
destroys the item, giving back only 35% of its crafting materials. The roll comes from a PRNG seeded with the item type,
the target level and your player name. With this mod:

- **Items at their maximum crafting level gain experience as you use them.** This covers every item the Forge of
  Potential can upgrade:
  - **Weapons and tools** gain experience on every hit that raises their own skill: melee hits, arrows and bolts that
    land, magic, and chopping or mining with the right tool.
  - **Shields** gain experience on every block. A parry (perfect block) gives 3× by default. If you block
    without a shield, the weapon you block with gets the experience.
  - **Armor** pieces you are wearing (helmet, chest, legs and cape) gain experience in two ways:
    - **Hits from attackers:** experience scales with the hit's damage, measured before your armor reduces it.
      Stronger armor doesn't slow its own leveling, and a hit that is partly blocked only counts the damage that got
      through the block.
    - **Blocks:** every block also gives each worn piece a share of the block's experience.
- **When the bar is full, the upgrade always succeeds** if you have the right idol.
- An item's experience shows in its tooltip. At the forge you also see the bar and the success chance.
- Items below their maximum crafting level can't be upgraded at the Forge of Potential. Upgrade them at their normal
  crafting station first.

Experience is stored in each item's custom data. It is saved with your character and stays with the item when it goes
into a chest or to another player. An upgrade starts the new level with an empty bar.

## Installation

- **Mod manager:** install with r2modman or Thunderstore Mod Manager. BepInExPack_Valheim is installed for you as a
  dependency.
- **Manual:** install [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), then
  copy `plugins/EquipmentExperience.dll` into `BepInEx/plugins/`.

The mod is client-side. You don't need it on the server, and other players don't need it.

## Configuration

Config file: `BepInEx/config/chrisrico.equipmentexperience.cfg`

| Section    | Key                           | Default           | Description |
|------------|-------------------------------|-------------------|-------------|
| Experience | `ExperienceRate`              | 1.0               | Global multiplier on all experience gained. |
| Experience | `WeaponExperienceRate`        | 1.0               | Experience per point of weapon or tool skill experience earned (about 1 per hit). |
| Experience | `ShieldExperienceRate`        | 1.0               | Experience per block, for the blocking item. |
| Experience | `ParryExperienceMultiplier`   | 3.0               | Multiplier on the blocking item's experience for a parry. |
| Experience | `ArmorExperiencePerDamage`    | 0.1               | Experience per point of damage in a hit from an attacker (before armor), for each worn piece. |
| Experience | `ArmorShareOfBlockExperience` | 0.5               | Fraction of a block's experience (before the parry bonus) that each worn piece also gains. |
| Leveling   | `BaseExperience`              | 500               | Experience needed for the first forge upgrade. |
| Leveling   | `ExperienceGrowth`            | 1.5               | Multiplier on the required experience for each further forge level. |
| Upgrading  | `AllowPartialUpgrade`         | false             | Allow upgrading before the bar is full. |
| Upgrading  | `PartialBaseChanceMultiplier` | 1.0               | Scales the idol's base chance used for partial upgrades. |
| General    | `NotifyWhenReady`             | true              | Show a message when an item's bar fills. |

### Partial upgrades

With `AllowPartialUpgrade = true`, you can try an upgrade before the bar is full:

```
base   = idolUpgradeChance * PartialBaseChanceMultiplier
chance = base + (1 - base) * fill        (fill = experience / required, from 0 to 1)
```

So the chance starts at the idol's own chance (65% for every vanilla idol) with an empty bar and rises in a straight
line to 100% with a full bar. Set `PartialBaseChanceMultiplier = 0` if you want the chance to be exactly how full the
bar is. The roll is truly random: it is not seeded, so you can't predict or replay it.

A failed attempt destroys the item, the same as a failed vanilla refinement. The idol and materials are used up, and
the game gives back 35% of the item's crafting materials.

### Multiplayer

The config is not synced from the server. Each player's own settings apply to their own items. Experience is stored on
the item itself, so an item keeps its experience when you give it to a player who doesn't have the mod. It only gains
more experience, or upgrades through experience, for players who have the mod.

## Compatibility with game updates

Most of the forge upgrade is still vanilla's own code. The mod decides whether the upgrade succeeds, then hands vanilla
a roll that forces that outcome, so anything Iron Gate adds to the upgrade process still runs.

The patches are applied in independent groups: weapon/shield experience, armor experience, the tooltip, the forge
upgrade, and the forge panel.

- **At startup**, each group checks that every game member it uses still exists. If an update renamed one, that group
  is turned off and the BepInEx log names the missing member and the game version. The other groups keep working.
- **If the forge upgrade group can't load**, the forge panel is turned off as well, so it can't promise a guaranteed
  upgrade. The log then says the vanilla random mechanic is active.
- **If a patch throws an error while the game is running**, the error is logged once and the game carries on. If the
  forge upgrade patch fails, the upgrade is blocked rather than handed back to the vanilla gamble.

## Building

You need the .NET SDK (6 or later) and a Valheim install with BepInEx.

```sh
dotnet build -c Release                     # produces bin/Release/EquipmentExperience.dll
dotnet build -c Release -p:Deploy=true      # also copies it into BepInEx/plugins/EquipmentExperience
dotnet build -c Release -p:ValheimDir=/path/to/Valheim   # if Valheim isn't in the default Steam location
dotnet build -c Release -p:Package=true     # also writes dist/EquipmentExperience-<version>.zip for Thunderstore
                                            # and the unzipped dist/EquipmentExperience-<version>/ folder
```

To release a new version:
1. Update `<Version>` in `EquipmentExperience.csproj`. The plugin version and the Thunderstore manifest both read it from
   there.
2. Add an entry to `CHANGELOG.md`.
3. Upload the zip.
