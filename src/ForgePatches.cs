using System;
using HarmonyLib;
using UnityEngine;

namespace WeaponExperience
{
    /// <summary>Whether an item can be upgraded at the forge right now, and with what chance.</summary>
    public readonly struct ForgeVerdict
    {
        public readonly bool Allowed;
        public readonly float Chance;
        public readonly string Reason;

        private ForgeVerdict(bool allowed, float chance, string reason)
        {
            Allowed = allowed;
            Chance = chance;
            Reason = reason;
        }

        public static ForgeVerdict Evaluate(ItemDrop.ItemData item, ItemDrop.ItemData idol)
        {
            if (!ItemExperience.IsAtMaxCraftingLevel(item))
            {
                return new ForgeVerdict(false, 0f, "Reach the maximum crafting level before using the Forge of Potential");
            }
            float fill = ItemExperience.GetFill(item);
            if (fill >= 1f)
            {
                return new ForgeVerdict(true, 1f, null);
            }
            if (!Plugin.AllowPartialUpgrade.Value)
            {
                return new ForgeVerdict(false, 0f, "Not enough experience to upgrade");
            }
            float baseChance = Mathf.Clamp01(idol.m_shared.m_upgradeChance * Plugin.PartialBaseChanceMultiplier.Value);
            return new ForgeVerdict(true, baseChance + (1f - baseChance) * fill, null);
        }
    }

    /// <summary>
    /// Decides the outcome of a forge upgrade from the item's experience, then lets vanilla carry it out.
    /// Vanilla's DoCrafting asks Player.GetItemUpgradeChance for a roll r and succeeds when
    /// idol.m_upgradeChance >= r, otherwise destroys the item when idol.m_breakChance >= 1 - r.
    /// We answer that call with a roll that forces the outcome we picked.
    /// </summary>
    public static class ForgeUpgradePatch
    {
        private const float SuccessRoll = 0f;
        private const float BreakRoll = 1.0001f;

        private static readonly System.Random Rng = new System.Random();
        private static float? _pendingRoll;

        public static PatchGroup Group()
        {
            return new PatchGroup("Forge of Potential upgrades")
                .With(ItemExperience.RequireCore)
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_craftRecipe))
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_craftUpgradeItem))
                .RequireField(typeof(CraftingStation), nameof(CraftingStation.m_upgrader))
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_upgradeChance))
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_breakChance))
                .Patch(typeof(ForgeUpgradePatch), typeof(InventoryGui), nameof(InventoryGui.DoCrafting),
                    new[] { typeof(Player) }, HarmonyPatchType.Prefix, nameof(Prefix))
                .Patch(typeof(ForgeUpgradePatch), typeof(InventoryGui), nameof(InventoryGui.DoCrafting),
                    new[] { typeof(Player) }, HarmonyPatchType.Finalizer, nameof(Finalizer))
                .Patch(typeof(ForgeUpgradePatch), typeof(Player), nameof(Player.GetItemUpgradeChance),
                    new[] { typeof(int) }, HarmonyPatchType.Prefix, nameof(UpgradeChancePrefix));
        }

        private static bool Prefix(InventoryGui __instance, Player player)
        {
            _pendingRoll = null;
            try
            {
                return Decide(__instance, player);
            }
            catch (Exception e)
            {
                // Never fall back to the vanilla gamble on an error: block the upgrade instead.
                Guard.Report("Forge upgrade", e);
                player.Message(MessageHud.MessageType.Center, "Weapon Experience error, upgrade blocked (see BepInEx log)");
                return false;
            }
        }

        /// <summary>Returns whether vanilla's DoCrafting should run.</summary>
        private static bool Decide(InventoryGui gui, Player player)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            ItemDrop.ItemData item = gui.m_craftUpgradeItem;
            if (gui.m_craftRecipe == null || item == null || station == null || !station.m_upgrader)
            {
                return true;
            }
            ItemDrop.ItemData idol = ItemExperience.GetIdol(gui.m_craftRecipe);
            if (idol == null)
            {
                return true; // vanilla reports the missing requirement
            }

            ForgeVerdict verdict = ForgeVerdict.Evaluate(item, idol);
            if (!verdict.Allowed)
            {
                player.Message(MessageHud.MessageType.Center, verdict.Reason);
                return false;
            }
            bool success = verdict.Chance >= 1f || Rng.NextDouble() < verdict.Chance;
            _pendingRoll = success ? SuccessRoll : BreakRoll;
            return true;
        }

        private static void Finalizer()
        {
            _pendingRoll = null;
        }

        private static bool UpgradeChancePrefix(ref float __result)
        {
            if (_pendingRoll == null)
            {
                return true;
            }
            __result = _pendingRoll.Value;
            _pendingRoll = null;
            return false;
        }
    }

    /// <summary>Shows experience and success chance in the forge and disables the button when not allowed.</summary>
    public static class ForgePanelPatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Forge of Potential panel")
                .With(ItemExperience.RequireCore)
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_selectedRecipe))
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_itemCraftType))
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_craftButton))
                .Patch(typeof(ForgePanelPatch), typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe),
                    new[] { typeof(Player), typeof(float) }, HarmonyPatchType.Postfix, nameof(Postfix));
        }

        private static void Postfix(InventoryGui __instance, Player player)
        {
            try
            {
                Update(__instance, player);
            }
            catch (Exception e)
            {
                Guard.Report("Forge of Potential panel", e);
            }
        }

        private static void Update(InventoryGui gui, Player player)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            ItemDrop.ItemData item = gui.m_selectedRecipe.ItemData;
            Recipe recipe = gui.m_selectedRecipe.Recipe;
            if (station == null || !station.m_upgrader || item == null || recipe == null)
            {
                return;
            }
            ItemDrop.ItemData idol = ItemExperience.GetIdol(recipe);
            if (idol == null)
            {
                return;
            }

            ForgeVerdict verdict = ForgeVerdict.Evaluate(item, idol);
            string text;
            if (!ItemExperience.IsAtMaxCraftingLevel(item))
            {
                text = $"<color=orange>{verdict.Reason}</color>";
            }
            else
            {
                text = $"Experience: {ItemExperience.FormatProgress(item)}\n";
                text += verdict.Allowed
                    ? $"Success chance: {Mathf.FloorToInt(verdict.Chance * 100f)}%"
                    : $"<color=orange>{verdict.Reason}</color>";
                if (verdict.Allowed && verdict.Chance < 1f)
                {
                    text += " <color=orange>(failure destroys the item)</color>";
                }
            }
            gui.m_itemCraftType.gameObject.SetActive(true);
            gui.m_itemCraftType.text = text;

            if (!verdict.Allowed)
            {
                gui.m_craftButton.interactable = false;
                gui.m_craftButton.GetComponent<UITooltip>().m_text = verdict.Reason;
            }
        }
    }
}
