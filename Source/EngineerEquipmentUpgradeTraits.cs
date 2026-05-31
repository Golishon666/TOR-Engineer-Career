using HarmonyLib;
using System.Linq;
using TOR_Core.Items;

namespace TOR_EngineerCareer
{
    internal static class EngineerEquipmentUpgradeTraits
    {
        public static void EnsureRegistered()
        {
            var traits = ItemTraitManager.Instance.GetItemTraits();
            if (traits == null)
            {
                return;
            }

            foreach (var upgrade in EngineerEquipmentUpgradeCatalog.All)
            {
                var existingTrait = traits.FirstOrDefault(trait => trait?.ItemTraitStringId == upgrade.Id);
                if (existingTrait != null)
                {
                    existingTrait.IconName = EngineerEquipmentUpgradeCatalog.UpgradeTraitIconName;
                    continue;
                }

                traits.Add(EngineerEquipmentUpgradeCatalog.CreateItemTrait(upgrade));
            }
        }
    }

    [HarmonyPatch(typeof(ItemTraitManager), nameof(ItemTraitManager.LoadItemTraits))]
    internal static class EngineerEquipmentUpgradeLoadTraitsPatch
    {
        private static void Postfix()
        {
            EngineerEquipmentUpgradeTraits.EnsureRegistered();
        }
    }
}
