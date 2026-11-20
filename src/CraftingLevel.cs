using System;
using System.Collections.Generic;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>
    /// The highest level an item can be crafted to at its station. Usually that's m_maxQuality, but vanilla requires
    /// station level m_minStationLevel + (quality - 1). For many Ashlands and Deep North items that's more than the
    /// station reaches with every extension built: Lightning Strike would need a level 6 Galdr table for quality 4,
    /// and the table stops at 5. Those items stop at 3. The stations are left alone; instead the Forge of Potential
    /// takes such items from the highest level they can actually be crafted to.
    /// </summary>
    public static class CraftingLevel
    {
        private const string Name = "Max crafting level";

        private static readonly Dictionary<string, int> ItemLevels = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> StationLevels = new Dictionary<string, int>();
        private static ObjectDB _cachedDb;
        private static int _cachedRecipeCount;
        private static ZNetScene _cachedScene;
        private static int _cachedPrefabCount;

        public static PatchGroup Require(PatchGroup group)
        {
            return group
                .RequireField(typeof(Recipe), nameof(Recipe.m_craftingStation))
                .RequireMethod(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel), typeof(int))
                .RequireMethod(typeof(Recipe), nameof(Recipe.GetRequiredStation), typeof(int))
                .RequireField(typeof(ZNetScene), nameof(ZNetScene.m_prefabs))
                .RequireField(typeof(StationExtension), nameof(StationExtension.m_craftingStation))
                .RequireField(typeof(StationExtension), nameof(StationExtension.m_stack));
        }

        /// <summary>
        /// The item's m_maxQuality, lowered to the highest quality its crafting station can reach with every extension
        /// built. Falls back to m_maxQuality when that can't be worked out (no world loaded, no crafting recipe, a
        /// station without extensions or with stacking ones).
        /// </summary>
        public static int Max(ItemDrop.ItemData item)
        {
            int maxQuality = item.m_shared.m_maxQuality;
            try
            {
                return Compute(item, maxQuality);
            }
            catch (Exception e)
            {
                Guard.Report(Name, e);
                return maxQuality;
            }
        }

        private static int Compute(ItemDrop.ItemData item, int maxQuality)
        {
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            if (db == null || scene == null || maxQuality <= 1)
            {
                return maxQuality;
            }
            // Other mods may register recipes or prefabs after the first lookup.
            if (scene != _cachedScene || scene.m_prefabs.Count != _cachedPrefabCount)
            {
                RebuildStations(scene);
                ItemLevels.Clear();
            }
            if (db != _cachedDb || db.m_recipes.Count != _cachedRecipeCount)
            {
                ItemLevels.Clear();
                _cachedDb = db;
                _cachedRecipeCount = db.m_recipes.Count;
            }
            string name = item.m_shared.m_name;
            if (!ItemLevels.TryGetValue(name, out int level))
            {
                level = Reachable(db, name, maxQuality);
                ItemLevels[name] = level;
            }
            return level;
        }

        private static int Reachable(ObjectDB db, string itemName, int maxQuality)
        {
            Recipe recipe = CraftingRecipe(db, itemName);
            CraftingStation station = recipe != null ? recipe.GetRequiredStation(maxQuality) : null;
            if (station == null || !StationLevels.TryGetValue(station.m_name, out int stationLevel) || stationLevel <= 0)
            {
                return maxQuality;
            }
            int quality = maxQuality;
            while (quality > 1 && recipe.GetRequiredStationLevel(quality) > stationLevel)
            {
                quality--;
            }
            return quality;
        }

        /// <summary>The item's normal recipe: one at a crafting station, without an idol.</summary>
        private static Recipe CraftingRecipe(ObjectDB db, string itemName)
        {
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe != null && recipe.m_item != null && recipe.m_item.m_itemData.m_shared.m_name == itemName
                    && recipe.m_craftingStation != null && !recipe.m_craftingStation.m_upgrader
                    && Experience.GetIdol(recipe) == null)
                {
                    return recipe;
                }
            }
            return null;
        }

        /// <summary>
        /// Each station's highest level: 1 plus one per distinct extension, as in StationExtension.FindExtensions.
        /// 0 for a station with a stacking extension, since then there is no limit. Counts every extension prefab,
        /// buildable or not; vanilla has no unbuildable ones.
        /// </summary>
        private static void RebuildStations(ZNetScene scene)
        {
            StationLevels.Clear();
            var extensions = new Dictionary<string, HashSet<string>>();
            var stacking = new HashSet<string>();
            foreach (GameObject prefab in scene.m_prefabs)
            {
                StationExtension extension = prefab != null ? prefab.GetComponent<StationExtension>() : null;
                if (extension == null || extension.m_craftingStation == null)
                {
                    continue;
                }
                string stationName = extension.m_craftingStation.m_name;
                if (extension.m_stack)
                {
                    stacking.Add(stationName);
                }
                if (!extensions.TryGetValue(stationName, out HashSet<string> names))
                {
                    extensions[stationName] = names = new HashSet<string>();
                }
                // FindExtensions counts extensions with the same piece name once.
                Piece piece = prefab.GetComponent<Piece>();
                names.Add(piece != null ? piece.m_name : prefab.name);
            }
            foreach (KeyValuePair<string, HashSet<string>> entry in extensions)
            {
                StationLevels[entry.Key] = stacking.Contains(entry.Key) ? 0 : 1 + entry.Value.Count;
            }
            _cachedScene = scene;
            _cachedPrefabCount = scene.m_prefabs.Count;
        }
    }
}
