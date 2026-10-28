# Changelog

## Unreleased

- Fixed: the Dead Raiser and Staff of Protection never gained experience. A Dead Raiser now gains experience when its
  skeletons hit something, and a Staff of Protection gains experience as its shield on you absorbs damage
  (`ShieldStaffExperiencePerShield`, 10 per full shield by default).
- Fixed: axes gained no experience from chopping trees, because chopping raises Wood Cutting rather than Axes.
- Pickaxe experience is no longer scaled by tier, so mining in a lower biome no longer earns less.

## 1.0.1

- The plugin GUID is now `nerdtastic.equipmentexperience`, so the config file is now
  `BepInEx/config/nerdtastic.equipmentexperience.cfg`. To keep your 1.0.0 settings, rename
  `chrisrico.equipmentexperience.cfg` to the new name before launching.

## 1.0.0

- First release. Forge-upgradable items gain experience from use, banking up to one bar before max crafting level,
  and a full bar at max level guarantees the Forge of Potential upgrade. Every upgrade awakens guardians from the idol's biome.
