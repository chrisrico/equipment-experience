using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>
    /// Experience bookkeeping for items. Experience lives in the item's m_customData, so it is saved with
    /// the character and travels with the item (chests, drops, other players).
    /// </summary>
    public static class Experience
    {
        public const string ExperienceKey = "EquipmentExperience.XP";

        private static readonly Dictionary<string, Recipe> ForgeRecipes = new Dictionary<string, Recipe>();
        private static ObjectDB _cachedDb;

        /// <summary>Game members everything in this class relies on, for <see cref="PatchGroup"/> startup checks.</summary>
        public static PatchGroup RequireCore(PatchGroup group)
        {
            return group
                .RequireField(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.m_customData))
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_maxQuality))
                .RequireField(typeof(ObjectDB), nameof(ObjectDB.m_recipes))
                .RequireField(typeof(Piece.Requirement), nameof(Piece.Requirement.m_upgraderResource));
        }

        /// <summary>The recipe that upgrades this item at the Forge of Potential, or null if there isn't one.</summary>
        public static Recipe GetForgeRecipe(ItemDrop.ItemData item)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || item?.m_shared == null)
            {
                return null;
            }
            if (db != _cachedDb)
            {
                ForgeRecipes.Clear();
                _cachedDb = db;
            }
            string name = item.m_shared.m_name;
            if (!ForgeRecipes.TryGetValue(name, out Recipe found))
            {
                foreach (Recipe recipe in db.m_recipes)
                {
                    if (recipe != null && recipe.m_item != null && recipe.m_item.m_itemData.m_shared.m_name == name
                        && GetIdol(recipe) != null)
                    {
                        found = recipe;
                        break;
                    }
                }
                ForgeRecipes[name] = found;
            }
            return found;
        }

        /// <summary>The idol (upgrader resource) required by a forge recipe.</summary>
        public static ItemDrop.ItemData GetIdol(Recipe recipe)
        {
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (requirement.m_upgraderResource && requirement.m_resItem != null)
                {
                    return requirement.m_resItem.m_itemData;
                }
            }
            return null;
        }

        public static bool IsAtMaxCraftingLevel(ItemDrop.ItemData item)
        {
            return item.m_shared.m_maxQuality > 1 && item.m_quality >= item.m_shared.m_maxQuality;
        }

        /// <summary>Whether this item accumulates experience: it is at max crafting level and forge-upgradable.</summary>
        public static bool CanGainExperience(ItemDrop.ItemData item)
        {
            return item?.m_shared != null && IsAtMaxCraftingLevel(item) && GetForgeRecipe(item) != null;
        }

        /// <summary>Experience needed to fill the bar at the item's current level.</summary>
        public static float GetRequired(ItemDrop.ItemData item)
        {
            int forgeLevel = Math.Max(0, item.m_quality - item.m_shared.m_maxQuality);
            return Plugin.BaseExperience.Value * Mathf.Pow(Plugin.ExperienceGrowth.Value, forgeLevel);
        }

        public static float GetExperience(ItemDrop.ItemData item)
        {
            if (item.m_customData != null && item.m_customData.TryGetValue(ExperienceKey, out string raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float xp))
            {
                return xp;
            }
            return 0f;
        }

        public static void SetExperience(ItemDrop.ItemData item, float xp)
        {
            if (item.m_customData == null)
            {
                item.m_customData = new Dictionary<string, string>();
            }
            item.m_customData[ExperienceKey] = xp.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>How full the experience bar is, 0..1.</summary>
        public static float GetFill(ItemDrop.ItemData item)
        {
            return Mathf.Clamp01(GetExperience(item) / GetRequired(item));
        }

        public static void AddExperience(ItemDrop.ItemData item, float amount)
        {
            amount *= Plugin.ExperienceRate.Value;
            if (amount <= 0f || !CanGainExperience(item))
            {
                return;
            }
            float required = GetRequired(item);
            float before = GetExperience(item);
            if (before >= required)
            {
                return;
            }
            float after = Mathf.Min(before + amount, required);
            SetExperience(item, after);

            if (after >= required && Plugin.NotifyWhenReady.Value && Player.m_localPlayer != null)
            {
                string name = Localization.instance.Localize(item.m_shared.m_name);
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    $"{name} is ready to be upgraded at the Forge of Potential", 0, item.GetIcon());
            }
        }

        public static string FormatProgress(ItemDrop.ItemData item)
        {
            float required = GetRequired(item);
            float xp = Mathf.Min(GetExperience(item), required);
            return $"{Mathf.FloorToInt(xp)} / {Mathf.CeilToInt(required)} ({Mathf.FloorToInt(xp / required * 100f)}%)";
        }
    }
}
