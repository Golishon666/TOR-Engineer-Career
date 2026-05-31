using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TOR_Core.CampaignMechanics.Crafting;
using TOR_Core.Extensions;
using TOR_Core.Items;
using TOR_Core.Utilities;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerEquipmentUpgradeCampaignBehavior : CampaignBehaviorBase
    {
        private const string MasterEngineerHubToken = "hub";

        private List<string> _learnedUpgradeIds = [];

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_torEngineerLearnedUpgradeIds", ref _learnedUpgradeIds);
            _learnedUpgradeIds ??= [];
        }

        public bool HasLearned(string upgradeId)
        {
            return _learnedUpgradeIds.Contains(upgradeId);
        }

        public IReadOnlyList<string> GetLearnedUpgradeIds()
        {
            return _learnedUpgradeIds;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "tor_engineer_upgrade_equipment_hub_p",
                MasterEngineerHubToken,
                "close_window",
                TORTextHelper.GetText("tor_engineer_upgrade_equipment_hub_p", "I want to upgrade my equipment."),
                CanOpenUpgradeEquipment,
                ShowUpgradeRoot,
                999);
        }

        private static bool CanOpenUpgradeEquipment()
        {
            var partner = CharacterObject.OneToOneConversationCharacter?.HeroObject;
            return partner?.IsMasterEngineer() == true &&
                   EngineerCareerHelper.IsEngineerHero(Hero.MainHero);
        }

        private void ShowUpgradeRoot()
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement("learn", "Buy blueprints", null, true, "Purchase engineering upgrade blueprints."),
                new InquiryElement("install", "Install upgrade", null, true, "Apply a learned upgrade to equipment for free.")
            };

            var inquiry = new MultiSelectionInquiryData(
                "Upgrade Equipment",
                "Choose what you want to do.",
                options,
                true,
                1,
                1,
                "Accept",
                "Cancel",
                elements =>
                {
                    var selected = elements.FirstOrDefault()?.Identifier as string;
                    if (selected == "learn")
                    {
                        ShowBlueprintShop();
                    }
                    else if (selected == "install")
                    {
                        ShowItemSelection();
                    }
                },
                null,
                "",
                true);

            MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
        }

        private void ShowBlueprintShop()
        {
            var options = new List<InquiryElement>();
            foreach (var upgrade in EngineerEquipmentUpgradeCatalog.All.Where(upgrade => !HasLearned(upgrade.Id)).OrderBy(upgrade => upgrade.Tier).ThenBy(upgrade => upgrade.Category).ThenBy(upgrade => upgrade.Name))
            {
                var enabled = CanBuyBlueprint(upgrade, out var reason);
                options.Add(new InquiryElement(
                    upgrade,
                    $"{upgrade.Name} (T{upgrade.Tier}, {EngineerEquipmentUpgradeCatalog.GetCategoryText(upgrade.Category)})",
                    null,
                    enabled,
                    BuildBlueprintHint(upgrade, reason)));
            }

            if (options.Count == 0)
            {
                ShowMessage("Upgrade Equipment", "You already know every engineering upgrade blueprint.");
                return;
            }

            var inquiry = new MultiSelectionInquiryData(
                "Upgrade Blueprints",
                "Blueprints cost gold, metals and one enchanting ingredient. Installing learned upgrades is free.",
                options,
                true,
                1,
                1,
                "Buy",
                "Cancel",
                BuyBlueprint,
                null,
                "",
                true);

            MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
        }

        private void BuyBlueprint(List<InquiryElement> elements)
        {
            var upgrade = elements.FirstOrDefault()?.Identifier as EngineerEquipmentUpgradeDefinition;
            if (upgrade == null)
            {
                ShowMessage("Upgrade Equipment", "Blueprint cannot be purchased.");
                return;
            }

            if (!CanBuyBlueprint(upgrade, out var reason))
            {
                ShowMessage("Upgrade Equipment", reason);
                return;
            }

            _learnedUpgradeIds.Add(upgrade.Id);
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, upgrade.GoldCost);

            var roster = MobileParty.MainParty.ItemRoster;
            roster.AddToCounts(upgrade.MetalItem, -upgrade.MetalCost);
            var ingredient = TorEnchantingIngredients.GetItemObjectForIngredient(upgrade.IngredientType);
            if (ingredient != null)
            {
                roster.AddToCounts(ingredient, -upgrade.IngredientCost);
            }

            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_upgrade_blueprint_learned}Blueprint learned: {BLUEPRINT}.")
                .SetTextVariable("BLUEPRINT", upgrade.Name));
        }

        private bool CanBuyBlueprint(EngineerEquipmentUpgradeDefinition upgrade, out string reason)
        {
            if (Hero.MainHero.GetSkillValue(DefaultSkills.Engineering) < upgrade.Tier * 100)
            {
                reason = $"Requires Engineering {upgrade.Tier * 100}.";
                return false;
            }

            if (Hero.MainHero.Gold < upgrade.GoldCost)
            {
                reason = "Not enough gold.";
                return false;
            }

            var roster = MobileParty.MainParty.ItemRoster;
            if (upgrade.MetalItem == null || roster.GetItemNumber(upgrade.MetalItem) < upgrade.MetalCost)
            {
                reason = $"Missing metal: {upgrade.MetalCost}x {upgrade.MetalItem?.Name}.";
                return false;
            }

            var ingredient = TorEnchantingIngredients.GetItemObjectForIngredient(upgrade.IngredientType);
            if (ingredient == null || roster.GetItemNumber(ingredient) < upgrade.IngredientCost)
            {
                reason = $"Missing ingredient: {upgrade.IngredientCost}x {ingredient?.Name}.";
                return false;
            }

            reason = "";
            return true;
        }

        private static string BuildBlueprintHint(EngineerEquipmentUpgradeDefinition upgrade, string disabledReason)
        {
            var metal = upgrade.MetalItem;
            var ingredient = TorEnchantingIngredients.GetItemObjectForIngredient(upgrade.IngredientType);
            var requirement = $"Requires Engineering {upgrade.Tier * 100}.";
            var goldIcon = "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";
            var cost = $"{upgrade.GoldCost}{goldIcon} + {upgrade.MetalCost}x {metal?.Name} + {upgrade.IngredientCost}x {ingredient?.Name}";
            return string.IsNullOrWhiteSpace(disabledReason)
                ? $"{upgrade.Description}\n{requirement}\nCost: {cost}"
                : $"{upgrade.Description}\n{requirement}\nCost: {cost}\n{disabledReason}";
        }

        private void ShowItemSelection()
        {
            var maxSlots = EngineerEquipmentUpgradeCatalog.GetMaxUpgradeSlots(Hero.MainHero);
            if (maxSlots <= 0)
            {
                ShowMessage("Upgrade Equipment", "Requires Engineering 100 to install the first upgrade.");
                return;
            }

            var known = GetLearnedUpgradeIds();
            if (known.Count == 0)
            {
                ShowMessage("Upgrade Equipment", "You do not know any engineering upgrade blueprints.");
                return;
            }

            var targets = GetUpgradeableTargets(known, maxSlots);
            if (targets.Count == 0)
            {
                ShowMessage("Upgrade Equipment", "No valid equipment can receive your known upgrades.");
                return;
            }

            var options = targets
                .Select(target => new InquiryElement(
                    target,
                    target.DisplayName,
                    new ItemImageIdentifier(target.Element.Item),
                    target.CanReceiveUpgrade,
                    BuildTargetHint(target, maxSlots)))
                .ToList();

            var inquiry = new MultiSelectionInquiryData(
                "Choose Item",
                $"Engineering allows {maxSlots} engineering upgrade(s) per item.",
                options,
                true,
                1,
                1,
                "Select",
                "Cancel",
                elements =>
                {
                    var target = elements.FirstOrDefault()?.Identifier as EngineerUpgradeTarget;
                    if (target != null)
                    {
                        ShowUpgradeSelection(target, known, maxSlots);
                    }
                },
                null,
                "",
                true);

            MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
        }

        private void ShowUpgradeSelection(EngineerUpgradeTarget target, IReadOnlyList<string> known, int maxSlots)
        {
            var currentIds = EngineerEquipmentUpgradeCatalog.GetEngineerUpgradeIds(target.Element.Item).ToHashSet();
            var currentCount = currentIds.Count;
            var options = new List<InquiryElement>();

            foreach (var upgrade in known.Select(EngineerEquipmentUpgradeCatalog.Get).Where(upgrade => upgrade != null).OrderBy(upgrade => upgrade.Tier).ThenBy(upgrade => upgrade.Category).ThenBy(upgrade => upgrade.Name))
            {
                var validForItem = EngineerEquipmentUpgradeCatalog.IsValidForItem(upgrade, target.Element.Item);
                var duplicate = currentIds.Contains(upgrade.Id);
                var hasSlot = currentCount < maxSlots;
                var enabled = validForItem && !duplicate && hasSlot;
                var reason = enabled ? "Install for free." :
                    duplicate ? "Already installed on this item." :
                    !hasSlot ? "No free engineering upgrade slots." :
                    "Wrong item type.";

                options.Add(new InquiryElement(
                    upgrade,
                    $"{upgrade.Name} (T{upgrade.Tier})",
                    null,
                    enabled,
                    $"{upgrade.Description}\n{reason}"));
            }

            var inquiry = new MultiSelectionInquiryData(
                "Choose Upgrade",
                target.DisplayName,
                options,
                true,
                1,
                1,
                "Install",
                "Cancel",
                elements =>
                {
                    var upgrade = elements.FirstOrDefault()?.Identifier as EngineerEquipmentUpgradeDefinition;
                    if (upgrade != null)
                    {
                        ApplyUpgrade(target, upgrade);
                    }
                },
                null,
                "",
                true);

            MBInformationManager.ShowMultiSelectionInquiry(inquiry, true);
        }

        private void ApplyUpgrade(EngineerUpgradeTarget target, EngineerEquipmentUpgradeDefinition upgrade)
        {
            var oldItem = target.Element.Item;
            var modifier = target.Element.ItemModifier;
            var existingTraitIds = oldItem.GetTorSpecificDataReadOnly()?.ItemTraits?.ToList() ?? [];

            if (existingTraitIds.Contains(upgrade.Id))
            {
                ShowMessage("Upgrade Equipment", "This upgrade is already installed on that item.");
                return;
            }

            existingTraitIds.Add(upgrade.Id);
            var newItem = EnchantmentHelper.CreateEnchantedItem(oldItem, existingTraitIds, oldItem.Name.ToString(), true, modifier);
            if (newItem == null)
            {
                ShowMessage("Upgrade Equipment", "Upgrade failed.");
                return;
            }

            var newElement = new EquipmentElement(newItem, modifier);
            if (target.IsEquipped)
            {
                Hero.MainHero.BattleEquipment.AddEquipmentToSlotWithoutAgent(target.EquipmentSlot, newElement);
            }
            else
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(newElement, 1);
                MobileParty.MainParty.ItemRoster.AddToCounts(target.Element, -1);
            }

            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_upgrade_installed}Installed {UPGRADE}.")
                .SetTextVariable("UPGRADE", upgrade.Name));
        }

        private static string BuildTargetHint(EngineerUpgradeTarget target, int maxSlots)
        {
            var currentIds = EngineerEquipmentUpgradeCatalog.GetEngineerUpgradeIds(target.Element.Item).ToList();
            var names = currentIds
                .Select(EngineerEquipmentUpgradeCatalog.Get)
                .Where(upgrade => upgrade != null)
                .Select(upgrade => upgrade.Name)
                .ToList();

            var upgradesText = names.Count == 0 ? "No engineering upgrades installed." : "Installed: " + string.Join(", ", names);
            return $"{upgradesText}\nSlots: {currentIds.Count}/{maxSlots}";
        }

        private List<EngineerUpgradeTarget> GetUpgradeableTargets(IReadOnlyList<string> knownUpgradeIds, int maxSlots)
        {
            var result = new List<EngineerUpgradeTarget>();
            var roster = MobileParty.MainParty.ItemRoster;

            foreach (var itemRosterElement in roster)
            {
                var element = itemRosterElement.EquipmentElement;
                if (element.IsEmpty || element.Item == null)
                {
                    continue;
                }

                if (knownUpgradeIds.Any(id => EngineerEquipmentUpgradeCatalog.IsValidForItem(EngineerEquipmentUpgradeCatalog.Get(id), element.Item)))
                {
                    result.Add(new EngineerUpgradeTarget(element, EquipmentIndex.None, false, itemRosterElement.Amount));
                }
            }

            var equipment = Hero.MainHero.BattleEquipment;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                var element = equipment[slot];
                if (element.IsEmpty || element.Item == null)
                {
                    continue;
                }

                if (knownUpgradeIds.Any(id => EngineerEquipmentUpgradeCatalog.IsValidForItem(EngineerEquipmentUpgradeCatalog.Get(id), element.Item)))
                {
                    result.Add(new EngineerUpgradeTarget(element, slot, true, 1));
                }
            }

            foreach (var target in result)
            {
                target.CanReceiveUpgrade = EngineerEquipmentUpgradeCatalog.ItemCanReceiveAnyKnownUpgrade(target.Element.Item, knownUpgradeIds, maxSlots);
            }

            return result
                .OrderByDescending(target => target.CanReceiveUpgrade)
                .ThenBy(target => target.IsEquipped ? 0 : 1)
                .ThenBy(target => target.DisplayName)
                .ToList();
        }

        private static void ShowMessage(string title, string text)
        {
            InformationManager.ShowInquiry(new InquiryData(title, text, true, false, "OK", null, null, null), true);
        }

        private sealed class EngineerUpgradeTarget
        {
            public EngineerUpgradeTarget(EquipmentElement element, EquipmentIndex equipmentSlot, bool isEquipped, int amount)
            {
                Element = element;
                EquipmentSlot = equipmentSlot;
                IsEquipped = isEquipped;
                Amount = amount;
            }

            public EquipmentElement Element { get; }
            public EquipmentIndex EquipmentSlot { get; }
            public bool IsEquipped { get; }
            public int Amount { get; }
            public bool CanReceiveUpgrade { get; set; }

            public string DisplayName
            {
                get
                {
                    var prefix = IsEquipped ? "[Equipped] " : Amount > 1 ? $"x{Amount} " : "";
                    return prefix + Element.Item.Name;
                }
            }
        }
    }
}
