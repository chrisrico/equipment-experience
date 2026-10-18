using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>
    /// Every Forge of Potential upgrade summons guardians matched to the idol's tier. Spawned creatures are owned by
    /// the upgrading client, and the game syncs them to other players like any other creature.
    /// </summary>
    public static class Guardians
    {
        /// <summary>Idol prefabs are named Upgrader{tier}{Weapon|Armor}, tier 0 (Wooden) to 7 (Bloodgold).</summary>
        private static readonly Regex IdolTier = new Regex(@"^Upgrader(\d+)");

        private static readonly string[] TierNames =
            { "Wooden", "Bronze", "Iron", "Silver", "BlackMetal", "BlackMarble", "Flametal", "Bloodgold" };

        private static readonly string[] DefaultGuardians =
        {
            "Greydwarf_Elite:2",                   // Wooden: Meadows / Black Forest
            "Troll:1",                             // Bronze: Black Forest
            "Abomination:1, Draugr_Elite:1",       // Iron: Swamp
            "StoneGolem:1",                        // Silver: Mountains
            "GoblinBrute:1, Goblin:2",             // Black Metal: Plains
            "SeekerBrute:1, Seeker:2",             // Black Marble: Mistlands
            "FallenValkyrie:1, Charred_Melee:1",   // Flametal: Ashlands
            "JotunWarrior:1, JotunWitch:1",        // Bloodgold: Deep North
        };

        private const int MaxStars = 2;
        private const float SpawnDelaySeconds = 2f;
        private const float MinSpawnDistance = 6f;
        private const float MaxSpawnDistance = 10f;
        private const float MaxSpawnHeightDifference = 4f;

        public static ConfigEntry<bool> Enabled;
        private static ConfigEntry<string>[] _tierGuardians;
        private static bool _available;

        public static void BindConfig(ConfigFile config)
        {
            Enabled = config.Bind("Guardians", "SpawnGuardians", true,
                "Summon guardians at the Forge of Potential whenever an item is refined.");
            _tierGuardians = new ConfigEntry<string>[TierNames.Length];
            for (int tier = 0; tier < TierNames.Length; tier++)
            {
                _tierGuardians[tier] = config.Bind("Guardians", $"Tier{tier}{TierNames[tier]}", DefaultGuardians[tier],
                    $"Guardians summoned when refining with a {TierNames[tier]} idol. Comma-separated " +
                    "Prefab:count or Prefab:count:stars entries. Stars rise by one for every two forge levels " +
                    $"past +1, up to {MaxStars}. Creatures from different factions fight each other, so keep each " +
                    "tier to one faction.");
            }
        }

        /// <summary>Checks the game members spawning relies on; without them upgrades still work, unguarded.</summary>
        public static PatchGroup Requirements()
        {
            return new PatchGroup("Forge guardians")
                .RequireMethod(typeof(ZNetScene), nameof(ZNetScene.GetPrefab), typeof(string))
                .RequireMethod(typeof(Character), nameof(Character.SetLevel), typeof(int))
                .RequireMethod(typeof(BaseAI), nameof(BaseAI.SetHuntPlayer), typeof(bool))
                .RequireMethod(typeof(BaseAI), nameof(BaseAI.Alert))
                .RequireMethod(typeof(ZoneSystem), nameof(ZoneSystem.GetSolidHeight),
                    typeof(Vector3), typeof(float).MakeByRefType(), typeof(int))
                .RequireField(typeof(ZoneSystem), nameof(ZoneSystem.m_waterLevel));
        }

        public static void SetAvailable(bool available)
        {
            _available = available;
        }

        /// <summary>The idol tier from the idol's prefab name, or -1 if it doesn't follow the vanilla naming.</summary>
        public static int GetTier(Recipe recipe)
        {
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement.m_upgraderResource && requirement.m_resItem != null)
                {
                    Match match = IdolTier.Match(requirement.m_resItem.gameObject.name);
                    return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
                }
            }
            return -1;
        }

        public static void Summon(int tier, int forgeLevel, Vector3 forgePosition)
        {
            if (!_available || !Enabled.Value || tier < 0 || tier >= _tierGuardians.Length)
            {
                return;
            }
            Plugin.Instance.StartCoroutine(SummonAfterDelay(tier, forgeLevel, forgePosition));
        }

        private static IEnumerator SummonAfterDelay(int tier, int forgeLevel, Vector3 forgePosition)
        {
            yield return new WaitForSeconds(SpawnDelaySeconds);
            try
            {
                Spawn(tier, forgeLevel, forgePosition);
            }
            catch (Exception e)
            {
                Guard.Report("Forge guardians", e);
            }
        }

        private static void Spawn(int tier, int forgeLevel, Vector3 forgePosition)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null)
            {
                return;
            }
            int bonusStars = Math.Max(0, forgeLevel - 1) / 2;
            int spawned = 0;
            foreach ((string prefabName, int count, int stars) in Parse(_tierGuardians[tier].Value))
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                if (prefab == null || prefab.GetComponent<Character>() == null)
                {
                    Plugin.Log.LogWarning($"Forge guardians: '{prefabName}' is not a creature prefab, skipping.");
                    continue;
                }
                for (int i = 0; i < count; i++)
                {
                    Vector3 position = FindSpawnPosition(forgePosition, player.transform.position);
                    Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up);
                    Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
                    GameObject go = UnityEngine.Object.Instantiate(prefab, position, facing);
                    go.GetComponent<Character>().SetLevel(1 + Math.Min(MaxStars, stars + bonusStars));
                    BaseAI ai = go.GetComponent<BaseAI>();
                    if (ai != null)
                    {
                        ai.SetHuntPlayer(true);
                        ai.Alert();
                    }
                    spawned++;
                }
            }
            if (spawned > 0)
            {
                player.Message(MessageHud.MessageType.Center, "The forge's guardians awaken!");
            }
        }

        private static IEnumerable<(string Prefab, int Count, int Stars)> Parse(string spec)
        {
            foreach (string entry in spec.Split(','))
            {
                string[] parts = entry.Trim().Split(':');
                if (parts[0].Length == 0)
                {
                    continue;
                }
                int count = 1, stars = 0;
                if ((parts.Length > 1 && !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    || (parts.Length > 2 && !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out stars)))
                {
                    Plugin.Log.LogWarning($"Forge guardians: can't parse '{entry.Trim()}', expected Prefab:count[:stars].");
                    continue;
                }
                yield return (parts[0], Mathf.Clamp(count, 0, 10), Mathf.Clamp(stars, 0, MaxStars));
            }
        }

        /// <summary>A dry spot on the ground in a ring around the forge, roughly level with it.</summary>
        private static Vector3 FindSpawnPosition(Vector3 forge, Vector3 fallback)
        {
            ZoneSystem zones = ZoneSystem.instance;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                float distance = UnityEngine.Random.Range(MinSpawnDistance, MaxSpawnDistance);
                Vector3 candidate = forge + new Vector3(dir.x, 0f, dir.y) * distance;
                if (zones.GetSolidHeight(candidate, out float height, 10)
                    && height > zones.m_waterLevel
                    && Mathf.Abs(height - forge.y) <= MaxSpawnHeightDifference)
                {
                    candidate.y = height + 0.5f;
                    return candidate;
                }
            }
            Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * MinSpawnDistance;
            return fallback + new Vector3(offset.x, 1f, offset.y);
        }
    }
}
