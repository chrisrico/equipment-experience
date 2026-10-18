using System;
using HarmonyLib;
using UnityEngine;

namespace EquipmentExperience
{
    /// <summary>Whether an item can be upgraded at the forge right now, and if not, why.</summary>
    public readonly struct ForgeVerdict
    {
        public readonly bool Allowed;
        public readonly string Reason;

        private ForgeVerdict(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason;
        }

        public static ForgeVerdict Evaluate(ItemDrop.ItemData item)
        {
            if (!Experience.IsAtMaxCraftingLevel(item))
            {
                return new ForgeVerdict(false, "Reach the maximum crafting level before using the Forge of Potential");
            }
            if (Experience.GetFill(item) < 1f)
            {
                return new ForgeVerdict(false, "Not enough experience to upgrade");
            }
            return new ForgeVerdict(true, null);
        }
    }

    /// <summary>
    /// Allows a forge upgrade only with a full experience bar, then lets vanilla carry it out and summons guardians.
    /// Vanilla's DoCrafting asks Player.GetItemUpgradeChance for a roll r and succeeds when idol.m_upgradeChance >= r;
    /// we answer that call with 0 so the upgrade always succeeds.
    /// </summary>
    public static class ForgeUpgradePatch
    {
        private const float SuccessRoll = 0f;

        private struct PendingUpgrade
        {
            public int IdolTier;
            public int ForgeLevel;
            public Vector3 ForgePosition;
            public bool Performed;
        }

        private static PendingUpgrade? _pending;

        public static PatchGroup Group()
        {
            return new PatchGroup("Forge of Potential upgrades")
                .With(Experience.RequireCore)
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_craftRecipe))
                .RequireField(typeof(InventoryGui), nameof(InventoryGui.m_craftUpgradeItem))
                .RequireField(typeof(CraftingStation), nameof(CraftingStation.m_upgrader))
                .RequireField(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_upgradeChance))
                .Patch(typeof(ForgeUpgradePatch), typeof(InventoryGui), nameof(InventoryGui.DoCrafting),
                    new[] { typeof(Player) }, HarmonyPatchType.Prefix, nameof(Prefix))
                .Patch(typeof(ForgeUpgradePatch), typeof(InventoryGui), nameof(InventoryGui.DoCrafting),
                    new[] { typeof(Player) }, HarmonyPatchType.Finalizer, nameof(Finalizer))
                .Patch(typeof(ForgeUpgradePatch), typeof(Player), nameof(Player.GetItemUpgradeChance),
                    new[] { typeof(int) }, HarmonyPatchType.Prefix, nameof(UpgradeChancePrefix));
        }

        private static bool Prefix(InventoryGui __instance, Player player)
        {
            _pending = null;
            try
            {
                return Decide(__instance, player);
            }
            catch (Exception e)
            {
                // Never fall back to the vanilla gamble on an error: block the upgrade instead.
                Guard.Report("Forge upgrade", e);
                player.Message(MessageHud.MessageType.Center, "Equipment Experience error, upgrade blocked (see BepInEx log)");
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
            if (Experience.GetIdol(gui.m_craftRecipe) == null)
            {
                return true; // vanilla reports the missing requirement
            }

            ForgeVerdict verdict = ForgeVerdict.Evaluate(item);
            if (!verdict.Allowed)
            {
                player.Message(MessageHud.MessageType.Center, verdict.Reason);
                return false;
            }
            _pending = new PendingUpgrade
            {
                IdolTier = Guardians.GetTier(gui.m_craftRecipe),
                ForgeLevel = item.m_quality + 1 - item.m_shared.m_maxQuality,
                ForgePosition = station.transform.position,
            };
            return true;
        }

        private static void Finalizer(Exception __exception)
        {
            PendingUpgrade? pending = _pending;
            _pending = null;
            if (__exception == null && pending is { Performed: true } upgrade)
            {
                Guardians.Summon(upgrade.IdolTier, upgrade.ForgeLevel, upgrade.ForgePosition);
            }
        }

        private static bool UpgradeChancePrefix(ref float __result)
        {
            if (_pending is not { Performed: false } upgrade)
            {
                return true;
            }
            upgrade.Performed = true;
            _pending = upgrade;
            __result = SuccessRoll;
            return false;
        }
    }

    /// <summary>Shows experience in the forge, warns about guardians, and disables the button when not allowed.</summary>
    public static class ForgePanelPatch
    {
        public static PatchGroup Group()
        {
            return new PatchGroup("Forge of Potential panel")
                .With(Experience.RequireCore)
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
            if (Experience.GetIdol(recipe) == null)
            {
                return;
            }

            ForgeVerdict verdict = ForgeVerdict.Evaluate(item);
            string text;
            if (!Experience.IsAtMaxCraftingLevel(item))
            {
                text = $"<color=orange>{verdict.Reason}</color>";
            }
            else
            {
                text = $"Experience: {Experience.FormatProgress(item)}\n";
                text += verdict.Allowed
                    ? Guardians.Enabled.Value ? "Ready. <color=orange>Refining will awaken the forge's guardians.</color>" : "Ready."
                    : $"<color=orange>{verdict.Reason}</color>";
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
