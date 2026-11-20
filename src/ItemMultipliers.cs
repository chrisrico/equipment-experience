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
        private static PrefabValues _values;

        public static void BindConfig(ConfigFile config)
        {
            _values = new PrefabValues(config.Bind("Experience", "ItemExperienceMultipliers", "StaffThunderBlood:5",
                "Comma-separated Prefab:multiplier entries for items whose experience should be scaled, on top of " +
                "every other rate (e.g. StaffThunderBlood:5 gives Lightning Strike five times its experience). " +
                "Use the item's prefab name, as used by the spawn command."));
        }

        /// <summary>The configured multiplier for this item, or 1.</summary>
        public static float For(ItemDrop.ItemData item)
        {
            return _values != null && _values.TryGet(item, out float multiplier) ? multiplier : 1f;
        }
    }
}
