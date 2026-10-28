using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>
    /// Biome tiers, 0 (Meadows) to 7 (Deep North), for items and enemies. Experience is scaled by how far the enemy's
    /// tier is above or below the item's. Every lookup fails neutral: anything unknown gives a multiplier of 1.
    /// </summary>
    public static class Tiers
    {
        public const int Unknown = -1;

        /// <summary>Idol prefabs are named Upgrader{tier}{Weapon|Armor}, tier 0 (Wooden) to 7 (Bloodgold).</summary>
        private static readonly Regex IdolPrefab = new Regex(@"^Upgrader(\d+)");

        /// <summary>
        /// Factions that only live in one biome, so their members keep that tier wherever they're met (a Fuling raid
        /// on a Meadows base is still a Plains fight). Matched by name so a reordered enum can't mislabel them.
        /// Other factions span several biomes and use the biome the enemy is standing in.
        /// </summary>
        private static readonly Dictionary<string, int> FactionTiers = new Dictionary<string, int>
        {
            ["MountainMonsters"] = 3,
            ["PlainsMonsters"] = 4,
            ["MistlandsMonsters"] = 5,
            ["Demon"] = 6,
            ["DeepNorth"] = 7,
        };

        private static ConfigEntry<float> _lowerTierMultiplier;
        private static ConfigEntry<float> _higherTierMultiplier;
        private static ConfigEntry<string> _enemyTierOverrides;
        private static string _parsedOverridesSource;
        private static Dictionary<string, int> _parsedOverrides = new Dictionary<string, int>();
        private static bool _available;

        // The highest-tier enemy the local player damaged or blocked this frame; vanilla raises the matching skill
        // in the same frame, which is where experience is awarded.
        private static int _targetTier = Unknown;
        private static int _targetFrame = -1;

        public static void BindConfig(ConfigFile config)
        {
            _lowerTierMultiplier = config.Bind("Tiers", "LowerTierMultiplier", 0.8f,
                new ConfigDescription(
                    "Experience multiplier for each biome the enemy is below the item's tier " +
                    "(0.8 = a Plains weapon earns 0.8^4 = 41% against Meadows enemies).",
                    new AcceptableValueRange<float>(0f, 1f)));
            _higherTierMultiplier = config.Bind("Tiers", "HigherTierMultiplier", 1.2f,
                new ConfigDescription(
                    "Experience multiplier for each biome the enemy is above the item's tier.",
                    new AcceptableValueRange<float>(1f, 3f)));
            _enemyTierOverrides = config.Bind("Tiers", "EnemyTierOverrides", "Surtling:2, GoblinDeepNorth:7",
                "Comma-separated Prefab:tier entries for creatures whose tier isn't the biome they're in or their " +
                "faction's home. Tiers: 0 Meadows, 1 Black Forest, 2 Swamp, 3 Mountains, 4 Plains, 5 Mistlands, " +
                "6 Ashlands, 7 Deep North.");
        }

        public static PatchGroup Group()
        {
            return new PatchGroup("Enemy tier tracking")
                .RequireField(typeof(Character), nameof(Character.m_faction))
                .RequireMethod(typeof(WorldGenerator), nameof(WorldGenerator.GetBiome), typeof(Vector3))
                .Patch(typeof(Tiers), typeof(Character), nameof(Character.Damage),
                    new[] { typeof(HitData) }, HarmonyPatchType.Prefix, nameof(DamagePrefix))
                .Patch(typeof(Tiers), typeof(Humanoid), nameof(Humanoid.BlockAttack),
                    new[] { typeof(HitData), typeof(Character) }, HarmonyPatchType.Prefix, nameof(BlockAttackPrefix));
        }

        public static void SetAvailable(bool available)
        {
            _available = available;
        }

        /// <summary>Experience multiplier for an item earning experience against an enemy of the given tier.</summary>
        public static float Multiplier(ItemDrop.ItemData item, int enemyTier)
        {
            int itemTier = _available ? ItemTier(item) : Unknown;
            if (itemTier == Unknown || enemyTier == Unknown)
            {
                return 1f;
            }
            int difference = enemyTier - itemTier;
            return difference < 0
                ? Mathf.Pow(_lowerTierMultiplier.Value, -difference)
                : Mathf.Pow(_higherTierMultiplier.Value, difference);
        }

        /// <summary>The idol tier of a forge recipe, or Unknown if its idol doesn't follow the vanilla naming.</summary>
        public static int IdolTier(Recipe recipe)
        {
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement.m_upgraderResource && requirement.m_resItem != null)
                {
                    Match match = IdolPrefab.Match(requirement.m_resItem.gameObject.name);
                    return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : Unknown;
                }
            }
            return Unknown;
        }

        public static int ItemTier(ItemDrop.ItemData item)
        {
            Recipe recipe = Experience.GetForgeRecipe(item);
            return recipe != null ? IdolTier(recipe) : Unknown;
        }

        public static int EnemyTier(Character enemy)
        {
            if (!_available || enemy == null)
            {
                return Unknown;
            }
            if (Overrides().TryGetValue(PrefabName(enemy.gameObject), out int overridden))
            {
                return overridden;
            }
            if (FactionTiers.TryGetValue(enemy.m_faction.ToString(), out int factionTier))
            {
                return factionTier;
            }
            return LocationTier(enemy.transform.position);
        }

        /// <summary>The tier of the biome at a world position; Ocean and anything unrecognised are Unknown.</summary>
        public static int LocationTier(Vector3 position)
        {
            if (!_available || WorldGenerator.instance == null)
            {
                return Unknown;
            }
            switch (WorldGenerator.instance.GetBiome(position))
            {
                case Heightmap.Biome.Meadows: return 0;
                case Heightmap.Biome.BlackForest: return 1;
                case Heightmap.Biome.Swamp: return 2;
                case Heightmap.Biome.Mountain: return 3;
                case Heightmap.Biome.Plains: return 4;
                case Heightmap.Biome.Mistlands: return 5;
                case Heightmap.Biome.AshLands: return 6;
                case Heightmap.Biome.DeepNorth: return 7;
                default: return Unknown;
            }
        }

        /// <summary>
        /// The tier of what the local player hit or blocked this frame. Hits on things that aren't creatures (trees,
        /// rocks) use the biome the player is standing in.
        /// </summary>
        public static int TakeTargetTier(Vector3 playerPosition)
        {
            int tier = _targetFrame == Time.frameCount && _targetTier != Unknown
                ? _targetTier
                : LocationTier(playerPosition);
            _targetTier = Unknown;
            _targetFrame = -1;
            return tier;
        }

        private static void DamagePrefix(Character __instance, HitData hit)
        {
            try
            {
                Player player = Player.m_localPlayer;
                if (player != null && __instance != player && hit.GetAttacker() == player)
                {
                    NoteTarget(EnemyTier(__instance));
                }
            }
            catch (Exception e)
            {
                Guard.Report("Enemy tier tracking", e);
            }
        }

        private static void BlockAttackPrefix(Humanoid __instance, Character attacker)
        {
            try
            {
                if (__instance == Player.m_localPlayer && attacker != null)
                {
                    NoteTarget(EnemyTier(attacker));
                }
            }
            catch (Exception e)
            {
                Guard.Report("Enemy tier tracking", e);
            }
        }

        private static void NoteTarget(int tier)
        {
            if (_targetFrame != Time.frameCount)
            {
                _targetFrame = Time.frameCount;
                _targetTier = Unknown;
            }
            _targetTier = Math.Max(_targetTier, tier);
        }

        private static Dictionary<string, int> Overrides()
        {
            string source = _enemyTierOverrides.Value;
            if (source == _parsedOverridesSource)
            {
                return _parsedOverrides;
            }
            var parsed = new Dictionary<string, int>();
            foreach (string entry in source.Split(','))
            {
                string[] parts = entry.Trim().Split(':');
                if (parts.Length == 2 && parts[0].Length > 0
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tier))
                {
                    parsed[parts[0]] = tier;
                }
                else if (entry.Trim().Length > 0)
                {
                    Plugin.Log.LogWarning($"EnemyTierOverrides: can't parse '{entry.Trim()}', expected Prefab:tier.");
                }
            }
            _parsedOverridesSource = source;
            _parsedOverrides = parsed;
            return parsed;
        }

        public static string PrefabName(GameObject go)
        {
            string name = go.name;
            int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return clone >= 0 ? name.Substring(0, clone).TrimEnd() : name;
        }
    }
}
