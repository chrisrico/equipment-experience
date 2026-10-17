# Weapon Experience

A BepInEx mod for Valheim that replaces the random Forge of Potential upgrade with an experience system.

In the base game, upgrading with an idol is a gamble. The roll comes from a PRNG seeded with the item, its level and your
player name, and a failed roll can destroy the item or take a level off it. With this mod:

- **Items at their maximum crafting level gain experience as you use them.** This covers every item the Forge of
  Potential can upgrade:
  - **Weapons and tools** gain experience on every hit that raises their skill. That includes melee hits, arrows and
    bolts that land, magic, and chopping or mining with an axe or pickaxe.
  - **Shields** gain experience on every successful block, and perfect blocks count double. If you block without a
    shield, the weapon you block with gets the experience.
  - **Armor** pieces you are wearing gain experience every time an attacker hits you.
- **When the bar is full, the upgrade always succeeds** if you have the right idol.
- An item's experience shows in its tooltip. At the forge you also see the bar and the success chance.
- Items below their maximum crafting level can't be upgraded at the Forge of Potential. Upgrade them at their normal
  crafting station first.

Experience is stored in each item's custom data. It is saved with your character and stays with the item when it goes
into a chest or to another player. An upgrade starts the new level with an empty bar.

## Configuration

Config file: `BepInEx/config/chrisrico.weaponexperience.cfg`

| Section    | Key                           | Default           | Description |
|------------|-------------------------------|-------------------|-------------|
| Experience | `ExperienceRate`              | 1.0               | Global multiplier on all experience gained. |
| Experience | `WeaponExperienceRate`        | 1.0               | Experience per point of weapon or tool skill experience earned (about 1 per hit). |
| Experience | `ShieldExperienceRate`        | 1.0               | Experience per block (2 for a perfect block). |
| Experience | `ArmorExperienceRate`         | 1.0               | Experience per hit taken, for each armor piece you are wearing. |
| Leveling   | `BaseExperience`              | 500               | Experience needed for the first forge upgrade. |
| Leveling   | `ExperienceGrowth`            | 1.5               | Multiplier on the required experience for each further forge level. |
| Upgrading  | `AllowPartialUpgrade`         | false             | Allow upgrading before the bar is full. |
| Upgrading  | `PartialBaseChanceMultiplier` | 1.0               | Scales the idol's base chance used for partial upgrades. |
| Upgrading  | `PartialFailurePenalty`       | `ResetExperience` | What a failed partial upgrade costs: `ResetExperience`, `None` or `Vanilla`. |
| General    | `NotifyWhenReady`             | true              | Show a message when an item's bar fills. |

### Partial upgrades

With `AllowPartialUpgrade = true`, you can try an upgrade before the bar is full:

```
base   = idolUpgradeChance * PartialBaseChanceMultiplier
chance = base + (1 - base) * fill        (fill = experience / required, from 0 to 1)
```

So the chance starts at the idol's own chance with an empty bar and rises in a straight line to 100% with a full bar.
Set `PartialBaseChanceMultiplier = 0` if you want the chance to be exactly how full the bar is. The roll is truly
random: it is not seeded, so you can't predict or replay it.

A failed attempt always uses up the idol and the materials. `PartialFailurePenalty` decides what else happens:

- `ResetExperience`: you keep the item, but its bar is emptied.
- `None`: you keep the item and its experience.
- `Vanilla`: the item is destroyed or loses a level, the same as in the base game.

### Multiplayer

The config is not synced from the server. Each player's own settings apply to their own items.

## Building

You need the .NET SDK (6 or later) and a Valheim install with BepInEx.

```sh
dotnet build -c Release                     # produces bin/Release/WeaponExperience.dll
dotnet build -c Release -p:Deploy=true      # also copies it into BepInEx/plugins/WeaponExperience
dotnet build -c Release -p:ValheimDir=/path/to/Valheim   # if Valheim isn't in the default Steam location
```
