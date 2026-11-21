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
  `ilspycmd` 8.x needs `DOTNET_ROLL_FORWARD=Major` to run on .NET 8. With only the .NET 8 SDK, a plain `dotnet tool install ilspycmd`
  fails (the latest needs a newer SDK); pin `--version 8.2.0.7535`.
- **`tools/release.sh` on this machine:** export `BepInExDir` (MSBuild reads it from the environment), `DOTNET_ROOT`
  and `PATH` for the scratchpad SDK, and `DOTNET_ROLL_FORWARD=Major`. The script's confirmation prompt reads stdin.
- **Reading item levels from a character save (`.fch`, under Steam `userdata/<id>/892970/remote/characters`):** items
  are stored compactly (`ItemData.Save`): an int for durability, then bytes for x, y, world level and flags. Optional
  fields follow, then the prefab name's `GetStableHashCode`, then the custom data. When scanning backwards from the
  `EquipmentExperience.XP` key, try every start offset and keep only the one that decodes cleanly. Taking the first
  match once misread a level 3 Lightning Strike as level 1.
- **Prefab data can be checked offline.** UnityPy reads the typetrees in
  `valheim_Data/StreamingAssets/SoftRef/Bundles`. That's how the staff attack chains were confirmed:
  StaffShield → `staff_shield_aoe` (an Aoe applying `Staff_shield`), and StaffSkeleton → `staff_skeleton_spawn`
  (a SpawnAbility spawning `Skeleton_Friendly`, which has levelUpFactor 0.5).
  - To find a prefab's bundle, grep `SoftRef/manifest_extended` for `path in bundle: .../<Name>.prefab`; the line
    before gives the bundle id. Most creatures, attacks and saddles are in `c4210710`.
  - UnityPy has no wheels for the system Python 3.15 and fails to build; use `uv venv -p 3.12`.
  - Reading a MonoBehaviour's `m_Script` fails (the script lives in another file), so identify components by their
    typetree field names instead (e.g. `m_fedDuration` = Tameable, `m_maxStamina` + `m_attachAnimation` = Sadle).
- **`Troll_Summoned` has no `Tameable`,** so nothing records who cast it (`SpawnAbility` only commands a Tameable) and
  vanilla gives no skill for its hits. `staff_greenroots_tentaroot` has none either.
- **Northern Vengeance's orbs raise skill `None`:** `SE_React.OnDamaged` launches them with a fresh `HitData`, and
  `Player.RaiseSkill` ignores `None`.

## Decisions

- 2026-11-19: Items stuck below max quality (vanilla needs station level `m_minStationLevel + quality - 1`; Galdr
  table (max 5) items with min 3 and Black Forge (max 6) items with min 4 top out at 3; 73 forge items, listed from
  the recipe data with UnityPy). **Don't touch the stations** (user's call): 1.0.5 capped the required station level
  via transpilers (fbbd574), and that was reverted. Instead `CraftingLevel.Max` lowers the item's "max crafting
  level" to what a fully extended station reaches, and the forge gate, experience curve and guardian level count
  from it. Vanilla's forge already accepts any quality and skips station level checks, so no forge patch was needed.

- 2026-11-06: Staff audit. Trollstav and Northern Vengeance earned nothing and are fixed. The rest get experience from
  their own hits, including Fracturing (splinters inherit hit data) and Echo Spike (the outbound hit only; the return
  projectile has no skill).
  - Trolls count by faction (`PlayerSpawned`) plus a Trollstav in the inventory. With no owner on record, a troll
    whose ZDO moved to another client credits that client's Trollstav. Stamping an owner on the ZDO would mean patching
    the `SpawnAbility.Spawn` coroutine, which isn't worth it for this edge case.
  - Northern Vengeance is credited when the orb fires, not when it lands: the orb's hit carries no staff information,
    and it flies straight at the attacker.
  - Staff of the Wild's roots deliberately earn nothing (no `Tameable`, faction `Players`): the staff already gains
    from its projectile's 3 m blast, as in vanilla.

- 2026-10-28: The Staff of Protection gains experience for each absorbed hit, as its share of the shield's capacity
  (`ShieldStaffExperiencePerShield`). Vanilla's single skill raise on a break would take about 500 shields per bar.
  Experience goes to the staff whose attack produces the effect. There's deliberately no fallback to the weapon in
  hand, because that would credit the wrong staff when the caster was stashed or belongs to another player.
