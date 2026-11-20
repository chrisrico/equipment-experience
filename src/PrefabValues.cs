using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace EquipmentExperience
{
    /// <summary>A config entry of comma-separated Prefab:value pairs, looked up by an item's prefab name.</summary>
    public sealed class PrefabValues
    {
        private readonly ConfigEntry<string> _config;
        private string _parsedSource;
        private Dictionary<string, float> _parsed = new Dictionary<string, float>();

        public PrefabValues(ConfigEntry<string> config)
        {
            _config = config;
        }

        public bool TryGet(ItemDrop.ItemData item, out float value)
        {
            value = 0f;
            string prefab = item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            return prefab != null && Parsed().TryGetValue(prefab, out value);
        }

        /// <summary>Reparsed whenever the config value changes, so edits apply without a restart.</summary>
        private Dictionary<string, float> Parsed()
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
                        $"{_config.Definition.Key}: can't parse '{entry.Trim()}', expected Prefab:value.");
                }
            }
            _parsedSource = source;
            _parsed = parsed;
            return parsed;
        }
    }
}
