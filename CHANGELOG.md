# Changelog

## 1.0.5

- Fixed: 73 items the Forge of Potential can upgrade, including most Deep North gear and Lightning Strike, could
  only be crafted to level 3, because level 4 needed a station level that no number of extensions reaches. The
  required level is now capped at the station's highest level, so they can reach level 4 and go to the forge.
- Lightning Strike gains 5× experience. It raises its skill by only 0.2 per cast, so it leveled about 6× slower
  than the Staff of Embers. The new `ItemExperienceMultipliers` setting can scale any item's experience by prefab name.

## 1.0.4

- Fixed: the Trollstav never gained experience. It now gains experience when its trolls hit something, at the same
  rate as other summoning staves.
- Fixed: Northern Vengeance never gained experience. It now gains experience each time its effect on you or a creature
  following you fires back at an attacker.

## 1.0.3

- The Staff of Protection also gains experience when its shield absorbs damage on a creature following you (your
  skeletons, tames), not only on you.

## 1.0.2

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
