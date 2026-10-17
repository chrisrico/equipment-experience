using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    /// <summary>Shows experience and success chance in the forge and disables the button when not allowed.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
    public static class ForgeRecipePanelPatch
    {
        private static void Postfix(InventoryGui __instance, Player player)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            ItemDrop.ItemData item = __instance.m_selectedRecipe.ItemData;
            Recipe recipe = __instance.m_selectedRecipe.Recipe;
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
                    text += $" <color=orange>({DescribeFailure()})</color>";
                }
            }
            __instance.m_itemCraftType.gameObject.SetActive(true);
            __instance.m_itemCraftType.text = text;

            if (!verdict.Allowed)
            {
                __instance.m_craftButton.interactable = false;
                __instance.m_craftButton.GetComponent<UITooltip>().m_text = verdict.Reason;
            }
        }

        private static string DescribeFailure()
        {
            switch (Plugin.PartialFailurePenalty.Value)
            {
                case FailurePenalty.ResetExperience:
                    return "failure resets experience";
                case FailurePenalty.Vanilla:
                    return "failure may destroy or downgrade the item";
                default:
                    return "failure costs only the materials";
            }
        }
    }

    /// <summary>Replaces the vanilla seeded-RNG forge upgrade with the experience-based one.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    public static class ForgeCraftPatch
    {
        private static bool Prefix(InventoryGui __instance, Player player)
        {
            CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
            if (__instance.m_craftRecipe == null || __instance.m_craftUpgradeItem == null || station == null || !station.m_upgrader)
            {
                return true;
            }
            DoForgeUpgrade(__instance, player, station);
            return false;
        }

        private static void DoForgeUpgrade(InventoryGui gui, Player player, CraftingStation station)
        {
            Recipe recipe = gui.m_craftRecipe;
            ItemDrop.ItemData item = gui.m_craftUpgradeItem;
            Inventory inventory = player.GetInventory();
            int newQuality = item.m_quality + 1;
            bool noCost = player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);

            int amount = recipe.GetAmount(newQuality, out int need, out ItemDrop.ItemData singleReqItem);
            if ((!player.HaveRequirements(recipe, false, newQuality) && !noCost)
                || !inventory.ContainsItem(item)
                || (recipe.m_requireOnlyOneIngredient && singleReqItem == null))
            {
                return;
            }

            ItemDrop.ItemData idol = ItemExperience.GetIdol(recipe);
            if (idol == null)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_missingrequirement");
                return;
            }

            ForgeVerdict verdict = ForgeVerdict.Evaluate(item, idol);
            if (!verdict.Allowed)
            {
                player.Message(MessageHud.MessageType.Center, verdict.Reason);
                return;
            }

            long playerID = player.GetPlayerID();
            string playerName = player.GetPlayerName();
            bool cheatedItems = inventory.ItemCheated(recipe.m_resources) || player.NoCostCheat();
            bool cheatedStation = station.GetComponent<ZNetView>().GetZDO().IsCheated();
            bool cheated = (cheatedItems || cheatedStation) && !PlayerProfile.s_bypassCheatChecks;
            string itemName = item.m_shared.m_name;
            string prefabName = recipe.m_item.gameObject.name;
            Vector2i position = item.m_gridPos;
            int variant = item.m_variant;

            bool success = verdict.Chance >= 1f || Random.value < verdict.Chance;
            if (success)
            {
                ReplaceItem(player, item, prefabName, amount, newQuality, variant, position, cheated);
                player.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize("$msg_upgrader_success", itemName, newQuality.ToString()), 0, null, log: true);
            }
            else
            {
                switch (Plugin.PartialFailurePenalty.Value)
                {
                    case FailurePenalty.ResetExperience:
                        ItemExperience.SetExperience(item, 0f);
                        player.Message(MessageHud.MessageType.Center,
                            Localization.instance.Localize(itemName) + " upgrade failed, experience lost", 0, null, log: true);
                        break;
                    case FailurePenalty.None:
                        player.Message(MessageHud.MessageType.Center,
                            Localization.instance.Localize(itemName) + " upgrade failed", 0, null, log: true);
                        break;
                    case FailurePenalty.Vanilla:
                        VanillaFailure(player, recipe, item, idol, prefabName, amount, variant, position, cheated);
                        break;
                }
            }

            if (!noCost)
            {
                if (singleReqItem != null)
                {
                    inventory.RemoveItem(singleReqItem.m_shared.m_name, need, singleReqItem.m_quality);
                }
                else
                {
                    player.ConsumeResources(recipe.m_resources, newQuality);
                }
            }
            gui.UpdateCraftingPanel();
            if (recipe.m_craftingStation != null && recipe.m_craftingStation.m_craftingSkill != Skills.SkillType.None)
            {
                player.RaiseSkill(recipe.m_craftingStation.m_craftingSkill);
            }

            EffectList effects = success ? station.m_craftItemDoneEffects : station.m_craftItemDoneFailEffects;
            effects.Create(player.transform.position, Quaternion.identity);

            PlayerProfile profile = Game.instance.GetPlayerProfile();
            profile.IncrementStat(PlayerStatType.CraftsOrUpgrades, 1f, cheatedItems);
            profile.IncrementStat(PlayerStatType.Upgrades, 1f, cheatedItems);
        }

        private static ItemDrop.ItemData ReplaceItem(Player player, ItemDrop.ItemData item, string prefabName, int amount,
            int quality, int variant, Vector2i position, bool cheated)
        {
            player.UnequipItem(item);
            player.GetInventory().RemoveItem(item);
            ItemDrop.ItemData replacement = player.GetInventory().AddItem(prefabName, amount, quality, variant,
                player.GetPlayerID(), player.GetPlayerName(), position, cheated);
            ItemExperience.CarryOverCustomData(item, replacement);
            return replacement;
        }

        /// <summary>
        /// Base game outcome of a failed upgrade, keeping vanilla's odds of breaking vs. downgrading
        /// conditional on having failed.
        /// </summary>
        private static void VanillaFailure(Player player, Recipe recipe, ItemDrop.ItemData item, ItemDrop.ItemData idol,
            string prefabName, int amount, int variant, Vector2i position, bool cheated)
        {
            string itemName = item.m_shared.m_name;
            float failChance = Mathf.Max(1f - idol.m_shared.m_upgradeChance, 0.0001f);
            bool broke = Random.value < idol.m_shared.m_breakChance / failChance;
            if (!broke)
            {
                ReplaceItem(player, item, prefabName, amount, item.m_quality - 1, variant, position, cheated);
                player.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize("$msg_upgrader_failed", itemName, (item.m_quality - 1).ToString()), 0, null, log: true);
                return;
            }

            player.UnequipItem(item);
            player.GetInventory().RemoveItem(item);
            player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$msg_upgrader_broke", itemName, item.m_quality.ToString()), 0, null, log: true);
            if (idol.m_shared.m_breakReturnIngreientsAmount <= 0f)
            {
                return;
            }
            foreach (Piece.Requirement requirement in recipe.m_resources)
            {
                if (!requirement.m_recover)
                {
                    continue;
                }
                int returned = Mathf.CeilToInt((requirement.GetAmount(1) + requirement.GetAmount(item.m_quality))
                                               * idol.m_shared.m_breakReturnIngreientsAmount);
                if (returned > 0)
                {
                    ItemDrop.ItemData res = requirement.m_resItem.m_itemData;
                    player.GetInventory().AddItem(requirement.m_resItem.name, returned, res.m_quality, res.m_variant,
                        player.GetPlayerID(), player.GetPlayerName(), new Vector2i(-1, -1), cheated);
                }
            }
        }
    }
}
