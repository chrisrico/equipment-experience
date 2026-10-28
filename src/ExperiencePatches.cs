using System;
using HarmonyLib;
using UnityEngine;

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
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_attack))
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_secondaryAttack))
                .RequireField(typeof(Attack), nameof(Attack.m_specialHitSkill))
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
                int enemyTier = Tiers.TakeTargetTier(player.transform.position);
                // Vanilla raises Blocking by 1 for a block and by more (2) for a parry.
                bool parry = value > 1f;
                float blockXp = Plugin.ShieldExperienceRate.Value;
                Experience.AddExperience(player.GetCurrentBlocker(),
                    parry ? blockXp * Plugin.ParryExperienceMultiplier.Value : blockXp, enemyTier);
                ArmorExperiencePatch.AddToWornArmor(player, blockXp * Plugin.ArmorShareOfBlockExperience.Value, enemyTier);
                return;
            }

            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (weapon != null && UsesSkill(weapon, skill))
            {
                int enemyTier = Tiers.TakeTargetTier(player.transform.position);
                // Ore is where it is, so mining in a lower biome isn't farming easy targets: pickaxes aren't scaled.
                if (weapon.m_shared.m_skillType == Skills.SkillType.Pickaxes)
                {
                    enemyTier = Tiers.Unknown;
                }
                Experience.AddExperience(weapon, value * Plugin.WeaponExperienceRate.Value, enemyTier);
            }
        }

        /// <summary>
        /// The weapon's own skill, or the skill its attacks raise on special targets instead (an axe hitting a tree
        /// raises Wood Cutting, not Axes).
        /// </summary>
        private static bool UsesSkill(ItemDrop.ItemData weapon, Skills.SkillType skill)
        {
            return weapon.m_shared.m_skillType == skill
                   || weapon.m_shared.m_attack?.m_specialHitSkill == skill
                   || weapon.m_shared.m_secondaryAttack?.m_specialHitSkill == skill;
        }
    }

    /// <summary>
    /// Armor gains experience in proportion to each hit's damage relative to the wearer's max health, so the pace is
    /// similar in every biome (enemy damage and player health rise together). Vanilla calls
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
                .RequireMethod(typeof(Character), nameof(Character.GetMaxHealth))
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

        public static void AddToWornArmor(Player player, float amount, int enemyTier)
        {
            Experience.AddExperience(player.m_helmetItem, amount, enemyTier);
            Experience.AddExperience(player.m_chestItem, amount, enemyTier);
            Experience.AddExperience(player.m_legItem, amount, enemyTier);
            Experience.AddExperience(player.m_shoulderItem, amount, enemyTier);
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
            float maxHealth = player.GetMaxHealth();
            if (maxHealth <= 0f)
            {
                return;
            }
            float healthFraction = Mathf.Min(damage / maxHealth, 1f);
            AddToWornArmor(player, healthFraction * Plugin.ArmorExperiencePerMaxHealth.Value, Tiers.EnemyTier(attacker));
        }

        /// <summary>The damage vanilla counts against armor durability.</summary>
        private static float ArmorDamage(HitData hit)
        {
            return hit.GetTotalPhysicalDamage() + hit.GetTotalElementalDamage();
        }
    }

    /// <summary>
    /// Keeps experience through normal crafting-station upgrades. Vanilla replaces the item with a new one at the same
    /// inventory slot and doesn't copy its custom data, so the experience is read before the upgrade and written onto
    /// the new item afterwards. Forge upgrades are left alone: they start the next level with an empty bar.
    /// </summary>
    public static class CraftingUpgradeCarryOverPatch
    {
        private struct Carried
        {
            public float Experience;
            public Vector2i Position;
            public string Name;
            public int Quality;
        }

        private static Carried? _carried;

        public static PatchGroup Group()
        {
            return new PatchGroup("Experience carry-over on crafting upgrades")
                .With(Experience.RequireCore)
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_craftUpgradeItem))
                .RequireField(typeof(CraftingStation), nameof(CraftingStation.m_upgrader))
                .RequireMethod(typeof(Inventory), nameof(Inventory.GetItemAt), typeof(int), typeof(int))
                .Patch(typeof(CraftingUpgradeCarryOverPatch), typeof(InventoryGui), nameof(InventoryGui.DoCrafting),
                    new[] { typeof(Player) }, HarmonyPatchType.Prefix, nameof(Prefix))
                .Patch(typeof(CraftingUpgradeCarryOverPatch), typeof(InventoryGui), nameof(InventoryGui.DoCrafting),
                    new[] { typeof(Player) }, HarmonyPatchType.Postfix, nameof(Postfix));
        }

        private static void Prefix(InventoryGui __instance, Player player)
        {
            _carried = null;
            try
            {
                Capture(__instance, player);
            }
            catch (Exception e)
            {
                Guard.Report("Experience carry-over on crafting upgrades", e);
            }
        }

        private static void Capture(InventoryGui gui, Player player)
        {
            ItemDrop.ItemData item = gui.m_craftUpgradeItem;
            CraftingStation station = player.GetCurrentCraftingStation();
            if (item == null || (station != null && station.m_upgrader))
            {
                return;
            }
            float xp = Experience.GetExperience(item);
            if (xp > 0f)
            {
                _carried = new Carried
                {
                    Experience = xp,
                    Position = item.m_gridPos,
                    Name = item.m_shared.m_name,
                    Quality = item.m_quality + 1,
                };
            }
        }

        private static void Postfix(Player player)
        {
            Carried? carried = _carried;
            _carried = null;
            if (carried == null)
            {
                return;
            }
            try
            {
                Restore(player, carried.Value);
            }
            catch (Exception e)
            {
                Guard.Report("Experience carry-over on crafting upgrades", e);
            }
        }

        private static void Restore(Player player, Carried carried)
        {
            ItemDrop.ItemData upgraded = player.GetInventory().GetItemAt(carried.Position.x, carried.Position.y);
            // Only if the upgrade happened: the slot now holds the same item one level higher.
            if (upgraded != null && upgraded.m_shared.m_name == carried.Name && upgraded.m_quality == carried.Quality)
            {
                Experience.SetExperience(upgraded, Mathf.Min(carried.Experience, Experience.GetRequired(upgraded)));
            }
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
