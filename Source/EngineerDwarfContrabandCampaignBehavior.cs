using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TOR_Core.CampaignMechanics.CustomResources;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerDwarfContrabandCampaignBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnSessionLaunched(CampaignGameStarter starter)
        {
            MBTextManager.SetTextVariable(
                "PRESTIGE_ICON",
                CustomResourceManager.GetResourceObject("Prestige").GetCustomResourceIconAsText());

            AddContrabandUpgradeDialog(starter);
        }

        private static void AddContrabandUpgradeDialog(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "tor_engineer_hub_dwarf_contraband_p",
                "hub",
                "tor_engineer_dwarf_contraband_shop",
                TORTextHelper.GetText("tor_engineer_hub_dwarf_contraband_p", "I have heard you can source contraband Dawi firearms. Can we arrange that?"),
                CanOpenContrabandUpgradeMenu,
                null,
                199);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_upgrade_1",
                "tor_engineer_dwarf_contraband_shop",
                "tor_engineer_dwarf_contraband_upgrade_1_response",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_upgrade_1", "For 500{PRESTIGE_ICON} I can move Dawi handguns, bullets and blasting charges."),
                () => !HasTier(1),
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_upgrade_2",
                "tor_engineer_dwarf_contraband_shop",
                "tor_engineer_dwarf_contraband_upgrade_2_response",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_upgrade_2", "Another 500{PRESTIGE_ICON} opens cannon, hand grenades, grudge-rakers, buckshot and drakefire pistols."),
                () => !HasTier(2) && HasTier(1),
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_upgrade_3",
                "tor_engineer_dwarf_contraband_shop",
                "tor_engineer_dwarf_contraband_upgrade_3_response",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_upgrade_3", "A final 500{PRESTIGE_ICON} secures masterwork handguns, Dronazgrund, trollhammers and torpedoes."),
                () => !HasTier(3) && HasTier(2),
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_complete",
                "tor_engineer_dwarf_contraband_shop",
                "hub",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_complete", "You already bought out every contraband contact I have. Browse my stock when you buy equipment."),
                () => HasTier(3),
                null,
                200);

            starter.AddPlayerLine(
                "tor_engineer_dwarf_contraband_agree_1",
                "tor_engineer_dwarf_contraband_upgrade_1_response",
                "tor_engineer_dwarf_contraband_upgrade_1_done",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_agree", "Very well. Grease the right palms (Spend 500{PRESTIGE_ICON})."),
                HasEnoughPrestige,
                () => UnlockTier(1),
                200);

            starter.AddPlayerLine(
                "tor_engineer_dwarf_contraband_agree_2",
                "tor_engineer_dwarf_contraband_upgrade_2_response",
                "tor_engineer_dwarf_contraband_upgrade_2_done",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_agree", "Very well. Grease the right palms (Spend 500{PRESTIGE_ICON})."),
                HasEnoughPrestige,
                () => UnlockTier(2),
                200);

            starter.AddPlayerLine(
                "tor_engineer_dwarf_contraband_agree_3",
                "tor_engineer_dwarf_contraband_upgrade_3_response",
                "tor_engineer_dwarf_contraband_upgrade_3_done",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_agree", "Very well. Grease the right palms (Spend 500{PRESTIGE_ICON})."),
                HasEnoughPrestige,
                () => UnlockTier(3),
                200);

            starter.AddPlayerLine(
                "tor_engineer_dwarf_contraband_decline_1",
                "tor_engineer_dwarf_contraband_upgrade_1_response",
                "tor_engineer_dwarf_contraband_decline",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_decline", "I cannot afford that right now."),
                null,
                null,
                200);

            starter.AddPlayerLine(
                "tor_engineer_dwarf_contraband_decline_2",
                "tor_engineer_dwarf_contraband_upgrade_2_response",
                "tor_engineer_dwarf_contraband_decline",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_decline", "I cannot afford that right now."),
                null,
                null,
                200);

            starter.AddPlayerLine(
                "tor_engineer_dwarf_contraband_decline_3",
                "tor_engineer_dwarf_contraband_upgrade_3_response",
                "tor_engineer_dwarf_contraband_decline",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_decline", "I cannot afford that right now."),
                null,
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_upgrade_1_done",
                "tor_engineer_dwarf_contraband_upgrade_1_done",
                "hub",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_upgrade_1_done", "The first crate is on its way. Ask to buy equipment when you are ready."),
                null,
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_upgrade_2_done",
                "tor_engineer_dwarf_contraband_upgrade_2_done",
                "hub",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_upgrade_2_done", "Good. The second shipment should keep your gunline supplied for a long campaign."),
                null,
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_upgrade_3_done",
                "tor_engineer_dwarf_contraband_upgrade_3_done",
                "hub",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_upgrade_3_done", "That is everything I dare move."),
                null,
                null,
                200);

            starter.AddDialogLine(
                "tor_engineer_dwarf_contraband_decline_response",
                "tor_engineer_dwarf_contraband_decline",
                "hub",
                TORTextHelper.GetText("tor_engineer_dwarf_contraband_decline_response", "Come back when your purse and your reputation can bear it."),
                null,
                null,
                200);
        }

        private static bool CanOpenContrabandUpgradeMenu()
        {
            return EngineerDwarfContrabandCatalog.CanOfferContraband(Hero.MainHero) &&
                   EngineerDwarfContrabandCatalog.GetUnlockedTierCount(Hero.MainHero) < EngineerDwarfContrabandCatalog.MaxTier;
        }

        private static bool HasTier(int tier)
        {
            return EngineerDwarfContrabandCatalog.HasTierUnlocked(Hero.MainHero, tier);
        }

        private static bool HasEnoughPrestige()
        {
            return EngineerDwarfContrabandCatalog.HasEnoughPrestige(Hero.MainHero);
        }

        private static void UnlockTier(int tier)
        {
            EngineerDwarfContrabandCatalog.UnlockTier(Hero.MainHero, tier);
        }
    }
}
