using System;
using HarmonyLib;

namespace EquipmentExperience
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
                .With(Experience.RequireCore)
                .RequireMethod(typeof(Humanoid), nameof(Humanoid.GetCurrentWeapon))
                .RequireMethod(typeof(Humanoid), nameof(Humanoid.GetCurrentBlocker))
                .With(ArmorExperiencePatch.RequireArmorSlots)
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
                // Vanilla raises Blocking by 1 for a block and by more (2) for a parry.
                bool parry = value > 1f;
                float blockXp = Plugin.ShieldExperienceRate.Value;
                Experience.AddExperience(player.GetCurrentBlocker(),
                    parry ? blockXp * Plugin.ParryExperienceMultiplier.Value : blockXp);
                ArmorExperiencePatch.AddToWornArmor(player, blockXp * Plugin.ArmorShareOfBlockExperience.Value);
                return;
            }

            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon != null && weapon.m_shared.m_skillType == skill)
            {
                Experience.AddExperience(weapon, value * Plugin.WeaponExperienceRate.Value);
            }
        }
    }

    /// <summary>
    /// Armor gains experience in proportion to the damage of each hit it takes. Vanilla calls
    /// Player.DamageArmorDurability right after HitData.ApplyArmor for every hit on a player, so the damage is
    /// recorded in ApplyArmor (before armor reduces it) and spent in DamageArmorDurability.
    /// </summary>
    public static class ArmorExperiencePatch
    {
        private static HitData _armorHit;
        private static float _damageBeforeArmor;

        public static PatchGroup Group()
        {
            return new PatchGroup("Armor experience")
                .With(Experience.RequireCore)
                .With(RequireArmorSlots)
                .Patch(typeof(ArmorExperiencePatch), typeof(HitData), nameof(HitData.ApplyArmor),
                    new[] { typeof(float) }, HarmonyPatchType.Prefix, nameof(ApplyArmorPrefix))
                .Patch(typeof(ArmorExperiencePatch), typeof(Player), nameof(Player.DamageArmorDurability),
                    new[] { typeof(HitData) }, HarmonyPatchType.Prefix, nameof(DamageArmorDurabilityPrefix));
        }

        public static PatchGroup RequireArmorSlots(PatchGroup group)
        {
            return group
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_helmetItem))
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_chestItem))
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_legItem))
                .RequireField(typeof(Humanoid), nameof(Humanoid.m_shoulderItem));
        }

        public static void AddToWornArmor(Player player, float amount)
        {
            Experience.AddExperience(player.m_helmetItem, amount);
            Experience.AddExperience(player.m_chestItem, amount);
            Experience.AddExperience(player.m_legItem, amount);
            Experience.AddExperience(player.m_shoulderItem, amount);
        }

        private static void ApplyArmorPrefix(HitData __instance)
        {
            try
            {
                _armorHit = __instance;
                _damageBeforeArmor = ArmorDamage(__instance);
            }
            catch (Exception e)
            {
                Guard.Report("Armor experience", e);
            }
        }

        private static void DamageArmorDurabilityPrefix(Player __instance, HitData hit)
        {
            try
            {
                Award(__instance, hit);
            }
            catch (Exception e)
            {
                Guard.Report("Armor experience", e);
            }
            finally
            {
                _armorHit = null;
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
            // Falls back to the post-armor damage if a game update separates the two calls.
            float damage = hit == _armorHit ? _damageBeforeArmor : ArmorDamage(hit);
            AddToWornArmor(player, damage * Plugin.ArmorExperiencePerDamage.Value);
        }

        /// <summary>The damage vanilla counts against armor durability.</summary>
        private static float ArmorDamage(HitData hit)
        {
            return hit.GetTotalPhysicalDamage() + hit.GetTotalElementalDamage();
        }
    }

    /// <summary>Shows the experience bar in the item tooltip.</summary>
    public static class TooltipPatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Tooltip experience bar")
                .With(Experience.RequireCore)
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
            if (crafting || !Experience.CanGainExperience(item))
            {
                return "";
            }
            string color = Experience.GetFill(item) >= 1f ? "#7FFF7F" : "orange";
            return $"\nExperience: <color={color}>{Experience.FormatProgress(item)}</color>";
        }
    }
}
