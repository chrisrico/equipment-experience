using HarmonyLib;

namespace WeaponExperience
{
    /// <summary>
    /// Weapons, tools and shields gain experience whenever the game awards the matching skill experience,
    /// i.e. exactly when vanilla considers the item "used successfully" (a hit, a chop, a block).
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
    public static class RaiseSkillPatch
    {
        private static void Prefix(Player __instance, Skills.SkillType skill, float value)
        {
            if (__instance != Player.m_localPlayer || skill == Skills.SkillType.None)
            {
                return;
            }

            if (skill == Skills.SkillType.Blocking)
            {
                ItemExperience.AddExperience(__instance.GetCurrentBlocker(), value * Plugin.ShieldExperienceRate.Value);
                return;
            }

            ItemDrop.ItemData weapon = __instance.GetCurrentWeapon();
            if (weapon != null && weapon.m_shared.m_skillType == skill)
            {
                ItemExperience.AddExperience(weapon, value * Plugin.WeaponExperienceRate.Value);
            }
        }
    }

    /// <summary>Armor gains experience for each hit the wearer takes from an attacker.</summary>
    [HarmonyPatch(typeof(Player), "OnDamaged")]
    public static class PlayerDamagedPatch
    {
        private static void Postfix(Player __instance, HitData hit)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker == __instance)
            {
                return;
            }

            float amount = Plugin.ArmorExperienceRate.Value;
            ItemExperience.AddExperience(__instance.m_helmetItem, amount);
            ItemExperience.AddExperience(__instance.m_chestItem, amount);
            ItemExperience.AddExperience(__instance.m_legItem, amount);
            ItemExperience.AddExperience(__instance.m_shoulderItem, amount);
        }
    }

    /// <summary>Shows the experience bar in the item tooltip.</summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    public static class TooltipPatch
    {
        private static void Postfix(ItemDrop.ItemData item, bool crafting, ref string __result)
        {
            if (crafting || !ItemExperience.CanGainExperience(item))
            {
                return;
            }
            string color = ItemExperience.GetFill(item) >= 1f ? "#7FFF7F" : "orange";
            __result += $"\nExperience: <color={color}>{ItemExperience.FormatProgress(item)}</color>";
        }
    }
}
