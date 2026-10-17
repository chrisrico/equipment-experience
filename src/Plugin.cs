using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace WeaponExperience
{
    public enum FailurePenalty
    {
        /// <summary>Keep the item, but its experience bar is emptied.</summary>
        ResetExperience,
        /// <summary>Keep the item and its experience; only the idol and materials are spent.</summary>
        None,
        /// <summary>Vanilla behaviour: the item is either destroyed or loses a level.</summary>
        Vanilla,
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "chrisrico.weaponexperience";
        public const string PluginName = "Weapon Experience";
        public const string PluginVersion = "1.0.0";

        public static ConfigEntry<float> ExperienceRate;
        public static ConfigEntry<float> WeaponExperienceRate;
        public static ConfigEntry<float> ShieldExperienceRate;
        public static ConfigEntry<float> ArmorExperienceRate;
        public static ConfigEntry<float> BaseExperience;
        public static ConfigEntry<float> ExperienceGrowth;
        public static ConfigEntry<bool> AllowPartialUpgrade;
        public static ConfigEntry<float> PartialBaseChanceMultiplier;
        public static ConfigEntry<FailurePenalty> PartialFailurePenalty;
        public static ConfigEntry<bool> NotifyWhenReady;

        private Harmony _harmony;

        private void Awake()
        {
            ExperienceRate = Config.Bind("Experience", "ExperienceRate", 1f,
                new ConfigDescription("Global multiplier applied to all experience gained by items.",
                    new AcceptableValueRange<float>(0f, 100f)));
            WeaponExperienceRate = Config.Bind("Experience", "WeaponExperienceRate", 1f,
                new ConfigDescription(
                    "Experience a weapon or tool gains per point of skill experience it earns you " +
                    "(i.e. every hit that would raise your weapon/tool skill).",
                    new AcceptableValueRange<float>(0f, 100f)));
            ShieldExperienceRate = Config.Bind("Experience", "ShieldExperienceRate", 1f,
                new ConfigDescription(
                    "Experience the blocking item (shield, or weapon if no shield) gains per successful block. " +
                    "Perfect blocks count double.",
                    new AcceptableValueRange<float>(0f, 100f)));
            ArmorExperienceRate = Config.Bind("Experience", "ArmorExperienceRate", 1f,
                new ConfigDescription("Experience each equipped armor piece gains whenever you take a hit from an attacker.",
                    new AcceptableValueRange<float>(0f, 100f)));

            BaseExperience = Config.Bind("Leveling", "BaseExperience", 500f,
                new ConfigDescription(
                    "Experience needed to fill the bar for the first Forge of Potential upgrade " +
                    "(an item at its maximum crafting level).",
                    new AcceptableValueRange<float>(1f, 1000000f)));
            ExperienceGrowth = Config.Bind("Leveling", "ExperienceGrowth", 1.5f,
                new ConfigDescription("Multiplier applied to the required experience for each further forge level.",
                    new AcceptableValueRange<float>(1f, 10f)));

            AllowPartialUpgrade = Config.Bind("Upgrading", "AllowPartialUpgrade", false,
                "Allow upgrading at the Forge of Potential before the experience bar is full. " +
                "Success is then not guaranteed: the chance starts at the idol's base chance and rises " +
                "linearly to 100% as the bar fills.");
            PartialBaseChanceMultiplier = Config.Bind("Upgrading", "PartialBaseChanceMultiplier", 1f,
                new ConfigDescription(
                    "Scales the idol's base success chance used for partial upgrades. " +
                    "1 = idol's normal chance at an empty bar; 0 = success chance equals how full the bar is.",
                    new AcceptableValueRange<float>(0f, 1f)));
            PartialFailurePenalty = Config.Bind("Upgrading", "PartialFailurePenalty", FailurePenalty.ResetExperience,
                "What happens when a partial upgrade fails. The idol and materials are always consumed. " +
                "ResetExperience: item kept, experience bar emptied. None: item and experience kept. " +
                "Vanilla: item is destroyed or loses a level, as in the base game.");
            NotifyWhenReady = Config.Bind("General", "NotifyWhenReady", true,
                "Show a message when an item's experience bar becomes full.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
