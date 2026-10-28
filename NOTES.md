# NOTES

Decision log and gotchas that git history doesn't capture.

## Gotchas

- **Not every skill gain goes through `Player.RaiseSkill`.** Some vanilla code calls `Skills.RaiseSkill` directly,
  and `WeaponExperiencePatch` never sees those calls. Blood magic does this in two places: `SE_Shield.IsDone` when a
  shield breaks, and `Character.RaiseSkill` on a tamed summon, which passes it on to its follow target. Dodge
  (`Player.cs`) does it too. `src/BloodMagicPatches.cs` covers the blood magic paths.
- **A weapon's hits don't always raise the weapon's own skill.** `Attack.m_specialHitSkill` replaces it on special
  targets: an axe hitting a tree raises Wood Cutting (13), not Axes (7). The weapon patch accepts either.
- `Character.RaiseSkill` on a summon also fires for its jumps (`Character.cs`, `RaiseSkill(Jump)`), and vanilla
  passes those on as Blood Magic. The summon patch ignores them.
- **Build environment on this machine:** there's no system .NET SDK; one was installed to a session scratchpad.
  BepInEx isn't in the Steam Valheim folder. It lives in the r2modman profile, so build with
  `-p:BepInExDir=~/.var/app/io.github.ebkr.r2modman/config/r2modmanPlus-local/Valheim/profiles/Default/BepInEx`.
  `ilspycmd` 8.x needs `DOTNET_ROLL_FORWARD=Major` to run on .NET 8.
- **Prefab data can be checked offline.** UnityPy reads the typetrees in
  `valheim_Data/StreamingAssets/SoftRef/Bundles`. That's how the staff attack chains were confirmed:
  StaffShield → `staff_shield_aoe` (an Aoe applying `Staff_shield`), and StaffSkeleton → `staff_skeleton_spawn`
  (a SpawnAbility spawning `Skeleton_Friendly`, which has levelUpFactor 0.5).

## Decisions

- 2026-10-28: The Staff of Protection gains experience for each absorbed hit, as its share of the shield's capacity
  (`ShieldStaffExperiencePerShield`). Vanilla's single skill raise on a break would take about 500 shields per bar.
  Experience goes to the staff whose attack produces the effect. There's deliberately no fallback to the weapon in
  hand, because that would credit the wrong staff when the caster was stashed or belongs to another player.
