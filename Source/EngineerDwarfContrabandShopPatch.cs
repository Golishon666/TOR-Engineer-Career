using System;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TOR_Core.CampaignSupport.TownBehaviours;

namespace TOR_EngineerCareer
{
    internal static class EngineerDwarfContrabandShopState
    {
        [ThreadStatic]
        private static bool _injectContraband;

        public static bool InjectContraband
        {
            get => _injectContraband;
            set => _injectContraband = value;
        }
    }

    [HarmonyPatch(typeof(MasterEngineerTownBehaviour), "opengunshopconsequence")]
    internal static class EngineerDwarfContrabandShopOpenPatch
    {
        private static void Prefix()
        {
            EngineerDwarfContrabandShopState.InjectContraband = true;
        }

        private static void Finalizer()
        {
            EngineerDwarfContrabandShopState.InjectContraband = false;
        }
    }

    [HarmonyPatch(typeof(InventoryScreenHelper), nameof(InventoryScreenHelper.OpenScreenAsTrade))]
    internal static class EngineerDwarfContrabandShopInventoryPatch
    {
        private static void Prefix(ItemRoster leftRoster)
        {
            if (!EngineerDwarfContrabandShopState.InjectContraband || leftRoster == null)
            {
                return;
            }

            EngineerDwarfContrabandCatalog.AppendUnlockedItemsToRoster(leftRoster, Hero.MainHero);
        }
    }
}
