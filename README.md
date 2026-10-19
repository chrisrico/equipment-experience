# Equipment Experience

A BepInEx mod for Valheim that replaces the random Forge of Potential upgrade with an experience system.

In the base game, refining an item with an idol is a gamble. Every idol succeeds 65% of the time, and every failure
destroys the item, giving back only 35% of its crafting materials. The roll comes from a PRNG seeded with the item type,
the target level and your player name. With this mod:

- **Items gain experience as you use them.** This covers every item the Forge of Potential can upgrade, from the
  moment it's crafted:
  - **Weapons and tools** gain experience on every hit that raises their own skill: melee hits, arrows and bolts that
    land, magic, and chopping or mining with the right tool.
  - **Shields** gain experience on every block. A parry (perfect block) gives 2× by default. If you block
    without a shield, the weapon you block with gets the experience.
  - **Armor** pieces you are wearing (helmet, chest, legs and cape) gain experience in two ways:
    - **Hits from attackers:** experience scales with how big the hit is compared with your max health. Enemy
      damage and your health rise together from biome to biome, so armor levels at a similar pace everywhere. Damage
      is measured before your armor reduces it, so stronger armor doesn't slow its own leveling. A hit that is partly
      blocked only counts the damage that got through the block.
    - **Blocks:** every block also gives each worn piece a share of the block's experience.
- **Experience depends on what you fight.** Enemies from a biome below the item's tier give less, and enemies from
  a biome above it give more, so farming easy targets is slow.
- **An upgrade needs a full bar, and then it always succeeds** if you have the right idol.
- **Every upgrade awakens the forge's guardians.** Creatures from the idol's biome spawn around the forge and attack.
- An item's experience shows in its tooltip. At the forge you also see the bar and the success chance.
- Items below their maximum crafting level can't be upgraded at the Forge of Potential. Upgrade them at their normal
  crafting station first. Experience earned before then counts toward the first forge upgrade, up to one full bar, and
  carries over through workbench upgrades. An item you've used since crafting it can reach max level already ready for
  the forge.

Experience is stored in each item's custom data. It is saved with your character and stays with the item when it goes
into a chest or to another player. A forge upgrade starts the new level with an empty bar.

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
| Experience | `ShieldExperienceRate`        | 2.0               | Experience per block, for the blocking item. |
| Experience | `ParryExperienceMultiplier`   | 2.0               | Multiplier on the blocking item's experience for a parry. |
| Experience | `ArmorExperiencePerMaxHealth` | 12                | Experience for each worn piece from a hit equal to your max health (smaller hits give a proportional share). |
| Experience | `ArmorShareOfBlockExperience` | 0.5               | Fraction of a block's experience (before the parry bonus) that each worn piece also gains. |
| Tiers      | `LowerTierMultiplier`         | 0.9               | Multiplier per biome the enemy is below the item's tier. |
| Tiers      | `HigherTierMultiplier`        | 1.1               | Multiplier per biome the enemy is above the item's tier. |
| Tiers      | `EnemyTierOverrides`          | `Surtling:2, GoblinDeepNorth:7` | `Prefab:tier` exceptions to the enemy tier rules. |
| Leveling   | `BaseExperience`              | 500               | Experience needed for the first forge upgrade. |
| Leveling   | `ExperienceGrowth`            | 1.5               | Multiplier on the required experience for each further forge level. |
| Guardians  | `SpawnGuardians`              | true              | Summon guardians whenever an item is refined. |
| Guardians  | `ForgeLevelsPerStar`          | 1                 | Forge levels per extra guardian star, up to 2 stars. |
| Guardians  | `ForgeLevelsPerExtraGuardian` | 2                 | After stars max out, forge levels per extra copy of each guardian entry. |
| Guardians  | `MaxExtraGuardians`           | 3                 | Most extra copies per guardian entry. |
| Guardians  | `Tier0Wooden` … `Tier7Bloodgold` | see below      | Which guardians each idol tier summons. |
| General    | `NotifyWhenReady`             | true              | Show a message when an item's bar fills. |

### Balance

The defaults are tuned so a weapon, a shield and each armor piece fill their bars at roughly the same pace while you
adventure. The estimates below are per enemy you fight at your gear's tier.

| Item | Where the experience comes from | XP per enemy |
|---|---|---|
| Weapon | about 4 hits landed × 1 XP | about 4 |
| Shield | 1 block × 2 XP, plus half a parry × 4 XP | about 4 |
| Armor, with a shield | 1.5 blocks × 1 XP, plus 0.75 hits taken × 35% of your health × 12 | about 4.6 |
| Armor, no shield | 1 hit taken × 35% of your health × 12 | about 4.2 |

Two figures here come from the game files: almost every weapon raises its skill by exactly 1 per hit, and every forge
recipe costs a single idol per level. Because the idol cost never rises, the experience curve is what makes each level
harder to reach:

| Forge level | XP needed | About how many enemies | Running total |
|---|---|---|---|
| +1 | 500   | 125 | 125   |
| +2 | 750   | 190 | 310   |
| +3 | 1,125 | 280 | 595   |
| +4 | 1,690 | 420 | 1,015 |
| +5 | 2,530 | 635 | 1,650 |
| +6 | 3,800 | 950 | 2,600 |

To level everything faster or slower, change `ExperienceRate`. To flatten or steepen the curve, change
`ExperienceGrowth`.

### Tiers

Items and enemies each have a biome tier: 0 Meadows, 1 Black Forest, 2 Swamp, 3 Mountains, 4 Plains, 5 Mistlands,
6 Ashlands, 7 Deep North. Every experience award is multiplied by `LowerTierMultiplier` for each tier the enemy is below
the item, or by `HigherTierMultiplier` for each tier above. Fighting at your gear's tier is unchanged.

| A Plains (tier 4) weapon fighting… | Multiplier |
|---|---|
| Meadows enemies | 0.9⁴ ≈ 0.66 |
| Swamp enemies | 0.9² = 0.81 |
| Plains enemies | 1 |
| Mistlands enemies | 1.1 |
| Ashlands enemies | 1.1² = 1.21 |

- **An item's tier** is the tier of the idol that refines it. Wooden idols are tier 0 and Bloodgold idols are tier 7.
- **An enemy's tier**, checked in this order:
  1. `EnemyTierOverrides`, if it lists the creature.
  2. Its faction, if that faction lives in only one biome: Mountains, Plains, Mistlands, Ashlands (Demon) or Deep North.
     A Fuling raid on a Meadows base still counts as a Plains fight.
  3. Otherwise, the biome the enemy is standing in. Greydwarfs, the undead, animals, bosses and Dvergr appear in several
     biomes, so their location decides. This also covers creatures added by other mods, with no list to maintain.
- **Hits on trees and rocks** use the biome you're standing in.
- **Blocks** use the attacker's tier. **Armor hits** also use the attacker's tier, and each piece compares it with its
  own tier.
- **Unknown cases count as 1×:** the ocean, a biome the mod doesn't recognise, or a non-vanilla idol.

### Guardians

Each refinement awakens the forge's guardians about two seconds after the upgrade finishes. They spawn 6–10 m from the
forge and hunt you straight away. The upgraded item is already yours: the fight is the price of the upgrade, not a
condition for it. Guardians drop their normal loot.

Which guardians appear depends on the idol's tier:

| Idol | Biome | Default guardians |
|---|---|---|
| Wooden | Meadows / Black Forest | 2 Greydwarf brutes |
| Bronze | Black Forest | Troll |
| Iron | Swamp | Abomination, Draugr elite |
| Silver | Mountains | Stone golem |
| Black Metal | Plains | Fuling berserker, 2 Fulings |
| Black Marble | Mistlands | Seeker soldier, 2 Seekers |
| Flametal | Ashlands | Fallen Valkyrie, Charred warrior |
| Bloodgold | Deep North | Jotun warrior, Jotun witch |

Guardians get tougher as the item does. They gain stars first, then numbers:

| Forge level | Stars | Extra copies of each entry | Example: Black Metal idol |
|---|---|---|---|
| +1 | 0 | 0 | 1 Fuling berserker, 2 Fulings |
| +2 | 1 | 0 | same, 1★ |
| +3 | 2 | 0 | same, 2★ |
| +4, +5 | 2 | 1 | 2 berserkers, 3 Fulings, 2★ |
| +6, +7 | 2 | 2 | 3 berserkers, 4 Fulings, 2★ |
| +8 and up | 2 | 3 (the cap) | 4 berserkers, 5 Fulings, 2★ |

A star adds one level to a creature: its health rises by its base amount, its damage by 50%, and its loot doubles.
Stars stop at 2, because loot keeps doubling with each level; after that, difficulty comes from numbers instead.

Each tier's setting is a comma-separated list of `Prefab:count` or `Prefab:count:stars` entries, giving the guardians at
+1. They use the game's prefab names, the same names the `spawn` console command takes. Keep each tier to one faction,
because creatures from different factions fight each other instead of you.

### Multiplayer

The config is not synced from the server. Each player's own settings apply to their own items. Experience is stored on
the item itself, so an item keeps its experience when you give it to a player who doesn't have the mod. It only gains
more experience, or upgrades through experience, for players who have the mod.

## Compatibility with game updates

Most of the forge upgrade is still vanilla's own code. The mod decides whether the upgrade succeeds, then hands vanilla
a roll that forces that outcome, so anything Iron Gate adds to the upgrade process still runs.

The patches are applied in independent groups: weapon/shield experience, armor experience, the tooltip, the forge
upgrade, the forge panel, experience carry-over on workbench upgrades, and enemy tier tracking. If tier tracking
fails, every tier multiplier is 1 and experience otherwise works as before. The guardians have their own startup check.
If it fails, upgrades still work, just without guardians. If the carry-over group fails, items only gain experience at
max crafting level, so a workbench upgrade can never wipe banked experience.

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
