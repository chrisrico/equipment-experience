using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>
    /// Lets every forge-upgradable item reach its max crafting level. Vanilla requires station level
    /// m_minStationLevel + (quality - 1), which for many Ashlands and Deep North items is more than the station can
    /// reach with every extension built (Lightning Strike needs a level 6 Galdr table for quality 4; the table stops
    /// at 5). Without max level they can never go to the Forge of Potential. The required level is capped at the
    /// station's highest reachable level, everywhere vanilla reads it: the craft check and the recipe panel.
    /// </summary>
    public static class StationLevelCap
    {
        private const string Name = "Station level cap for forge items";

        private static readonly MethodInfo VanillaRequiredLevel =
            AccessTools.Method(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel), new[] { typeof(int) });

        private static readonly MethodInfo CappedRequiredLevel =
            AccessTools.Method(typeof(StationLevelCap), nameof(RequiredLevel));

        private static readonly Dictionary<string, int> MaxLevels = new Dictionary<string, int>();
        private static ZNetScene _cachedScene;
        private static int _cachedPrefabCount;

        public static PatchGroup Group()
        {
            Type[] requiredStationArgs = { typeof(Recipe), typeof(int), typeof(bool) };
            Type[] updateRecipeArgs = { typeof(Player), typeof(float) };
            return new PatchGroup(Name)
                .With(Experience.RequireCore)
                .RequireMethod(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel), typeof(int))
                .RequireMethod(typeof(Recipe), nameof(Recipe.GetRequiredStation), typeof(int))
                .RequireField(typeof(ZNetScene), nameof(ZNetScene.m_prefabs))
                .RequireField(typeof(StationExtension), nameof(StationExtension.m_craftingStation))
                .RequireField(typeof(StationExtension), nameof(StationExtension.m_stack))
                .RequireCall(typeof(Player), nameof(Player.RequiredCraftingStation), requiredStationArgs, VanillaRequiredLevel)
                .RequireCall(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe), updateRecipeArgs, VanillaRequiredLevel)
                .Patch(typeof(StationLevelCap), typeof(Player), nameof(Player.RequiredCraftingStation),
                    requiredStationArgs, HarmonyPatchType.Transpiler, nameof(Transpiler))
                .Patch(typeof(StationLevelCap), typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe),
                    updateRecipeArgs, HarmonyPatchType.Transpiler, nameof(Transpiler));
        }

        /// <summary>
        /// Replaces calls to Recipe.GetRequiredStationLevel rather than patching it: it's small enough for Mono to
        /// inline into its callers, which would skip a patch on the method itself.
        /// </summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(VanillaRequiredLevel))
                {
                    // Same stack shape: (Recipe, int) -> int.
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = CappedRequiredLevel;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced == 0)
            {
                // Makes harmony.Patch throw, so PatchGroup.Apply undoes the group instead of leaving it half-applied.
                throw new InvalidOperationException("no call to Recipe.GetRequiredStationLevel found");
            }
        }

        public static int RequiredLevel(Recipe recipe, int quality)
        {
            int required = recipe.GetRequiredStationLevel(quality);
            try
            {
                return Cap(recipe, quality, required);
            }
            catch (Exception e)
            {
                Guard.Report(Name, e);
                return required;
            }
        }

        private static int Cap(Recipe recipe, int quality, int required)
        {
            ItemDrop.ItemData item = recipe.m_item != null ? recipe.m_item.m_itemData : null;
            if (item == null || Experience.GetForgeRecipe(item) == null)
            {
                return required;
            }
            CraftingStation station = recipe.GetRequiredStation(quality);
            if (station == null || !TryGetMaxLevel(station, out int maxLevel))
            {
                return required;
            }
            // Only upgrades are eased: crafting the item still needs the recipe's own minimum level.
            return Mathf.Max(Mathf.Min(required, maxLevel), recipe.m_minStationLevel);
        }

        /// <summary>
        /// The highest level a station can reach: 1 plus one per distinct extension, as in
        /// StationExtension.FindExtensions. Fails for a station with no extensions, or if any of its extensions stack,
        /// since then there is no limit to cap at. Counts every extension prefab, buildable or not; vanilla has no
        /// unbuildable ones.
        /// </summary>
        private static bool TryGetMaxLevel(CraftingStation station, out int maxLevel)
        {
            maxLevel = 0;
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
            {
                return false;
            }
            // Other mods may register prefabs after the first lookup.
            if (scene != _cachedScene || scene.m_prefabs.Count != _cachedPrefabCount)
            {
                Rebuild(scene);
            }
            // Stations without extensions are left alone.
            return MaxLevels.TryGetValue(station.m_name, out maxLevel) && maxLevel > 0;
        }

        private static void Rebuild(ZNetScene scene)
        {
            MaxLevels.Clear();
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
                // 0 marks "no limit", which TryGetMaxLevel treats as unknown.
                MaxLevels[entry.Key] = stacking.Contains(entry.Key) ? 0 : 1 + entry.Value.Count;
            }
            _cachedScene = scene;
            _cachedPrefabCount = scene.m_prefabs.Count;
        }
    }
}
