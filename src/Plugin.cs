using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace EquipmentExperience
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "chrisrico.equipmentexperience";
        public const string PluginName = "Equipment Experience";
        public const string PluginVersion = PluginInfo.Version;

        public static ConfigEntry<float> ExperienceRate;
        public static ConfigEntry<float> WeaponExperienceRate;
        public static ConfigEntry<float> ShieldExperienceRate;
        public static ConfigEntry<float> ParryExperienceMultiplier;
        public static ConfigEntry<float> ArmorExperiencePerMaxHealth;
        public static ConfigEntry<float> ArmorShareOfBlockExperience;
        public static ConfigEntry<float> BaseExperience;
        public static ConfigEntry<float> ExperienceGrowth;
        public static ConfigEntry<bool> AllowPartialUpgrade;
        public static ConfigEntry<float> PartialBaseChanceMultiplier;
        public static ConfigEntry<bool> NotifyWhenReady;

        public static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ExperienceRate = Config.Bind("Experience", "ExperienceRate", 1f,
                new ConfigDescription("Global multiplier applied to all experience gained by items.",
                    new AcceptableValueRange<float>(0f, 100f)));
            WeaponExperienceRate = Config.Bind("Experience", "WeaponExperienceRate", 1f,
                new ConfigDescription(
                    "Experience a weapon or tool gains per point of skill experience it earns you " +
                    "(i.e. every hit that would raise your weapon/tool skill).",
                    new AcceptableValueRange<float>(0f, 100f)));
            ShieldExperienceRate = Config.Bind("Experience", "ShieldExperienceRate", 2f,
                new ConfigDescription(
                    "Experience the blocking item (shield, or weapon if no shield) gains per block.",
                    new AcceptableValueRange<float>(0f, 100f)));
            ParryExperienceMultiplier = Config.Bind("Experience", "ParryExperienceMultiplier", 2f,
                new ConfigDescription(
                    "Multiplier on the blocking item's experience for a parry (perfect block).",
                    new AcceptableValueRange<float>(1f, 100f)));
            ArmorExperiencePerMaxHealth = Config.Bind("Experience", "ArmorExperiencePerMaxHealth", 12f,
                new ConfigDescription(
                    "Experience each equipped armor piece gains from a hit by an attacker that deals your full max " +
                    "health in damage; smaller hits give a proportional share (a hit for 25% of your health gives a " +
                    "quarter). Damage is measured after any block but before armor, and capped at your max health. " +
                    "Scaling by max health keeps armor leveling at a similar pace in every biome.",
                    new AcceptableValueRange<float>(0f, 1000f)));
            ArmorShareOfBlockExperience = Config.Bind("Experience", "ArmorShareOfBlockExperience", 0.5f,
                new ConfigDescription(
                    "Fraction of a block's experience (before the parry bonus) that each equipped armor piece also gains.",
                    new AcceptableValueRange<float>(0f, 10f)));

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
                "linearly to 100% as the bar fills. A failed attempt destroys the item.");
            PartialBaseChanceMultiplier = Config.Bind("Upgrading", "PartialBaseChanceMultiplier", 1f,
                new ConfigDescription(
                    "Scales the idol's base success chance used for partial upgrades. " +
                    "1 = idol's normal chance at an empty bar; 0 = success chance equals how full the bar is.",
                    new AcceptableValueRange<float>(0f, 1f)));
            NotifyWhenReady = Config.Bind("General", "NotifyWhenReady", true,
                "Show a message when an item's experience bar becomes full.");

            _harmony = new Harmony(PluginGuid);
            WeaponExperiencePatch.Group().Apply(_harmony);
            ArmorExperiencePatch.Group().Apply(_harmony);
            TooltipPatch.Group().Apply(_harmony);
            if (ForgeUpgradePatch.Group().Apply(_harmony))
            {
                // The panel promises guaranteed upgrades, so only show it when the forge logic is active.
                ForgePanelPatch.Group().Apply(_harmony);
            }
            else
            {
                Log.LogError("The Forge of Potential is using the vanilla random upgrade mechanic.");
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
