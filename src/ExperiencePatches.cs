using System;
using HarmonyLib;

namespace WeaponExperience
{
    /// <summary>
    /// Weapons, tools and shields gain experience whenever the game awards the matching skill experience,
    /// i.e. exactly when vanilla considers the item "used successfully" (a hit, a chop, a block).
    /// </summary>
    public static class WeaponExperiencePatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Weapon and shield experience")
                .With(ItemExperience.RequireCore)
                .RequireMethod(typeof(Humanoid), nameof(Humanoid.GetCurrentWeapon))
                .RequireMethod(typeof(Humanoid), nameof(Humanoid.GetCurrentBlocker))
                .Patch(typeof(WeaponExperiencePatch), typeof(Player), nameof(Player.RaiseSkill),
                    new[] { typeof(Skills.SkillType), typeof(float) }, HarmonyPatchType.Prefix, nameof(Prefix));
        }

        private static void Prefix(Player __instance, Skills.SkillType skill, float value)
        {
            try
            {
                Award(__instance, skill, value);
            }
            catch (Exception e)
            {
                Guard.Report("Weapon and shield experience", e);
            }
        }

        private static void Award(Player player, Skills.SkillType skill, float value)
        {
            if (player != Player.m_localPlayer || skill == Skills.SkillType.None)
            {
                return;
            }

            if (skill == Skills.SkillType.Blocking)
            {
                ItemExperience.AddExperience(player.GetCurrentBlocker(), value * Plugin.ShieldExperienceRate.Value);
                return;
            }

            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon != null && weapon.m_shared.m_skillType == skill)
            {
                ItemExperience.AddExperience(weapon, value * Plugin.WeaponExperienceRate.Value);
            }
        }
    }

    /// <summary>Armor gains experience for each hit the wearer takes from an attacker.</summary>
    public static class ArmorExperiencePatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Armor experience")
                .With(ItemExperience.RequireCore)
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_helmetItem))
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_chestItem))
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_legItem))
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_shoulderItem))
                .Patch(typeof(ArmorExperiencePatch), typeof(Player), "OnDamaged",
                    new[] { typeof(HitData) }, HarmonyPatchType.Postfix, nameof(Postfix));
        }

        private static void Postfix(Player __instance, HitData hit)
        {
            try
            {
                Award(__instance, hit);
            }
            catch (Exception e)
            {
                Guard.Report("Armor experience", e);
            }
        }

        private static void Award(Player player, HitData hit)
        {
            if (player != Player.m_localPlayer)
            {
                return;
            }
            Character attacker = hit.GetAttacker();
            if (attacker == null || attacker == player)
            {
                return;
            }

            float amount = Plugin.ArmorExperienceRate.Value;
            ItemExperience.AddExperience(player.m_helmetItem, amount);
            ItemExperience.AddExperience(player.m_chestItem, amount);
            ItemExperience.AddExperience(player.m_legItem, amount);
            ItemExperience.AddExperience(player.m_shoulderItem, amount);
        }
    }

    /// <summary>Shows the experience bar in the item tooltip.</summary>
    public static class TooltipPatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Tooltip experience bar")
                .With(ItemExperience.RequireCore)
                .Patch(typeof(TooltipPatch), typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
                    new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) },
                    HarmonyPatchType.Postfix, nameof(Postfix));
        }

        private static void Postfix(ItemDrop.ItemData item, bool crafting, ref string __result)
        {
            try
            {
                __result += Describe(item, crafting);
            }
            catch (Exception e)
            {
                Guard.Report("Tooltip experience bar", e);
            }
        }

        private static string Describe(ItemDrop.ItemData item, bool crafting)
        {
            if (crafting || !ItemExperience.CanGainExperience(item))
            {
                return "";
            }
            string color = ItemExperience.GetFill(item) >= 1f ? "#7FFF7F" : "orange";
            return $"\nExperience: <color={color}>{ItemExperience.FormatProgress(item)}</color>";
        }
    }
}
