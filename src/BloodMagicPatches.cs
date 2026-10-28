using System;
using HarmonyLib;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>
    /// Blood magic staves whose skill experience doesn't come from their own hits: vanilla raises the skill straight on
    /// the player's Skills, bypassing Player.RaiseSkill, so <see cref="WeaponExperiencePatch"/> never sees it. The
    /// experience goes to the staff that casts the effect, even if it is no longer in hand.
    /// </summary>
    public static class BloodMagic
    {
        public static PatchGroup RequireCasterLookup(PatchGroup group)
        {
            return group
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_attack))
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_secondaryAttack))
                .RequireField(typeof(Attack), nameof(Attack.m_attackProjectile))
                .RequireMethod(typeof(Humanoid), nameof(Humanoid.GetCurrentWeapon))
                .RequireMethod(typeof(Inventory), nameof(Inventory.GetAllItems));
        }

        /// <summary>
        /// The item that casts an effect: the weapon in hand if one of its attacks launches a matching prefab, otherwise
        /// the first such item in the inventory, or null if the caster isn't on you (stashed, or another player's).
        /// </summary>
        public static ItemDrop.ItemData FindCaster(Player player, Func<GameObject, bool> casts)
        {
            ItemDrop.ItemData weapon = player.GetCurrentWeapon();
            if (Casts(weapon, casts))
            {
                return weapon;
            }
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (Casts(item, casts))
                {
                    return item;
                }
            }
            return null;
        }

        private static bool Casts(ItemDrop.ItemData item, Func<GameObject, bool> casts)
        {
            if (item?.m_shared == null)
            {
                return false;
            }
            GameObject primary = item.m_shared.m_attack?.m_attackProjectile;
            GameObject secondary = item.m_shared.m_secondaryAttack?.m_attackProjectile;
            return (primary != null && casts(primary)) || (secondary != null && casts(secondary));
        }
    }

    /// <summary>
    /// Summoning staves (Dead Raiser) gain experience when their summons hit something. Vanilla passes the summon's
    /// skill experience to its owner in Character.RaiseSkill, scaled by Tameable.m_levelUpFactor.
    /// </summary>
    public static class SummonExperiencePatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Summoning staff experience")
                .With(Experience.RequireCore)
                .With(BloodMagic.RequireCasterLookup)
                .RequireField(typeof(Tameable), nameof(Tameable.m_levelUpOwnerSkill))
                .RequireField(typeof(Tameable), nameof(Tameable.m_levelUpFactor))
                .RequireField(typeof(SpawnAbility), nameof(SpawnAbility.m_spawnPrefab))
                .RequireMethod(typeof(MonsterAI), nameof(MonsterAI.GetFollowTarget))
                .RequireMethod(typeof(MonsterAI), nameof(MonsterAI.GetTargetCreature))
                .Patch(typeof(SummonExperiencePatch), typeof(Character), nameof(Character.RaiseSkill),
                    new[] { typeof(Skills.SkillType), typeof(float) }, HarmonyPatchType.Prefix, nameof(Prefix));
        }

        private static void Prefix(Character __instance, Skills.SkillType skill, float value)
        {
            try
            {
                // Vanilla also passes a summon's jumps on to its owner; only its attacks count here.
                if (skill != Skills.SkillType.Jump)
                {
                    Award(__instance, value);
                }
            }
            catch (Exception e)
            {
                Guard.Report("Summoning staff experience", e);
            }
        }

        private static void Award(Character summon, float value)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !summon.IsTamed())
            {
                return;
            }
            Tameable tameable = summon.GetComponent<Tameable>();
            MonsterAI ai = summon.GetComponent<MonsterAI>();
            if (tameable == null || ai == null || tameable.m_levelUpOwnerSkill == Skills.SkillType.None
                || ai.GetFollowTarget() != player.gameObject)
            {
                return;
            }
            string prefab = Tiers.PrefabName(summon.gameObject);
            ItemDrop.ItemData staff = BloodMagic.FindCaster(player, projectile => Spawns(projectile, prefab));
            Character target = ai.GetTargetCreature();
            int enemyTier = target != null ? Tiers.EnemyTier(target) : Tiers.LocationTier(summon.transform.position);
            Experience.AddExperience(staff, value * tameable.m_levelUpFactor * Plugin.WeaponExperienceRate.Value,
                enemyTier);
        }

        private static bool Spawns(GameObject projectile, string prefab)
        {
            SpawnAbility spawner = projectile.GetComponent<SpawnAbility>();
            if (spawner == null || spawner.m_spawnPrefab == null)
            {
                return false;
            }
            foreach (GameObject spawned in spawner.m_spawnPrefab)
            {
                if (spawned != null && spawned.name == prefab)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Shield staves (Staff of Protection) gain experience as the shield they cast on you or a creature following you
    /// (summons, tames) absorbs damage. Vanilla only raises the skill once, when the shield breaks, and only for the
    /// shielded character; awarding each absorbed hit's share of the shield's capacity gives the same total for a
    /// broken shield and also counts shields that expire first. Shields on other players run on their client and
    /// aren't seen here.
    /// </summary>
    public static class ProtectionExperiencePatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Shield staff experience")
                .With(Experience.RequireCore)
                .With(BloodMagic.RequireCasterLookup)
                .RequireField(typeof(StatusEffect), nameof(StatusEffect.m_character))
                .RequireField(typeof(SE_Shield), nameof(SE_Shield.m_levelUpSkillOnBreak))
                .RequireField(typeof(SE_Shield), nameof(SE_Shield.m_levelUpSkillFactor))
                .RequireField(typeof(SE_Shield), nameof(SE_Shield.m_totalAbsorbDamage))
                .RequireField(typeof(SE_Shield), nameof(SE_Shield.m_damage))
                .RequireField(typeof(Aoe), nameof(Aoe.m_statusEffect))
                .RequireField(typeof(Aoe), nameof(Aoe.m_statusEffectIfPlayer))
                .RequireMethod(typeof(StatusEffect), nameof(StatusEffect.NameHash))
                .RequireMethod(typeof(MonsterAI), nameof(MonsterAI.GetFollowTarget))
                .Patch(typeof(ProtectionExperiencePatch), typeof(SE_Shield), nameof(SE_Shield.OnDamaged),
                    new[] { typeof(HitData), typeof(Character) }, HarmonyPatchType.Prefix, nameof(Prefix));
        }

        private static void Prefix(SE_Shield __instance, HitData hit, Character attacker)
        {
            try
            {
                Award(__instance, hit, attacker);
            }
            catch (Exception e)
            {
                Guard.Report("Shield staff experience", e);
            }
        }

        private static void Award(SE_Shield shield, HitData hit, Character attacker)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !IsYoursToProtect(shield.m_character, player)
                || shield.m_levelUpSkillOnBreak == Skills.SkillType.None || shield.m_totalAbsorbDamage <= 0f)
            {
                return;
            }
            // Runs before vanilla adds this hit to m_damage, so only the capacity still left counts.
            float absorbed = Mathf.Min(hit.GetTotalDamage(), shield.m_totalAbsorbDamage - shield.m_damage);
            if (absorbed <= 0f)
            {
                return;
            }
            int effect = shield.NameHash();
            ItemDrop.ItemData staff = BloodMagic.FindCaster(player, projectile => Applies(projectile, effect));
            float share = absorbed / shield.m_totalAbsorbDamage;
            Experience.AddExperience(staff,
                share * shield.m_levelUpSkillFactor * Plugin.ShieldStaffExperiencePerShield.Value,
                Tiers.EnemyTier(attacker));
        }

        private static bool IsYoursToProtect(Character shielded, Player player)
        {
            if (shielded == player)
            {
                return true;
            }
            if (shielded == null || !shielded.IsTamed())
            {
                return false;
            }
            MonsterAI ai = shielded.GetComponent<MonsterAI>();
            return ai != null && ai.GetFollowTarget() == player.gameObject;
        }

        private static bool Applies(GameObject projectile, int effect)
        {
            Aoe aoe = projectile.GetComponent<Aoe>();
            return aoe != null && (Is(aoe.m_statusEffect, effect) || Is(aoe.m_statusEffectIfPlayer, effect));
        }

        private static bool Is(string statusEffect, int effect)
        {
            return !string.IsNullOrEmpty(statusEffect) && statusEffect.GetStableHashCode() == effect;
        }
    }
}
