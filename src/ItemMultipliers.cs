using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace EquipmentExperience
{
    /// <summary>
    /// Per-item experience multipliers, keyed by item prefab name. Some items earn far less than others for the same
    /// effort because vanilla gives their attack a small skill raise (Lightning Strike raises 0.2 per cast for 40 eitr,
    /// the Staff of Embers 1.0 for 35).
    /// </summary>
    public static class ItemMultipliers
    {
        private static ConfigEntry<string> _config;
        private static string _parsedSource;
        private static Dictionary<string, float> _parsed = new Dictionary<string, float>();

        public static void BindConfig(ConfigFile config)
        {
            _config = config.Bind("Experience", "ItemExperienceMultipliers", "StaffThunderBlood:5",
                "Comma-separated Prefab:multiplier entries for items whose experience should be scaled, on top of " +
                "every other rate (e.g. StaffThunderBlood:5 gives Lightning Strike five times its experience). " +
                "Use the item's prefab name, as used by the spawn command.");
        }

        /// <summary>The configured multiplier for this item, or 1.</summary>
        public static float For(ItemDrop.ItemData item)
        {
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            return prefab != null && _config != null && Parsed().TryGetValue(prefab, out float multiplier)
                ? multiplier
                : 1f;
        }

        private static Dictionary<string, float> Parsed()
        {
            string source = _config.Value;
            if (source == _parsedSource)
            {
                return _parsed;
            }
            var parsed = new Dictionary<string, float>();
            foreach (string entry in source.Split(','))
            {
                string[] parts = entry.Trim().Split(':');
                if (parts.Length == 2 && parts[0].Trim().Length > 0
                    && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    && value >= 0f && !float.IsInfinity(value))
                {
                    parsed[parts[0].Trim()] = value;
                }
                else if (entry.Trim().Length > 0)
                {
                    Plugin.Log.LogWarning(
                        $"ItemExperienceMultipliers: can't parse '{entry.Trim()}', expected Prefab:multiplier.");
                }
            }
            _parsedSource = source;
            _parsed = parsed;
            return parsed;
        }
    }
}
