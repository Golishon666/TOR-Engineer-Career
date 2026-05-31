using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal static class EngineerEquipmentUpgradeHelper
    {
        private static readonly Dictionary<int, HashSet<string>> LastGrenadeUpgradeIdsByAgent = [];
        private static readonly EquipmentIndex[] EquipmentUpgradeSlots =
        [
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3,
            EquipmentIndex.ExtraWeaponSlot,
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Cape,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg
        ];
        private static readonly EquipmentIndex[] AgentEquipmentUpgradeSlots =
        [
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3,
            EquipmentIndex.ExtraWeaponSlot,
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Cape,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg
        ];

        public static int CountEquippedFieldMedkits(Hero hero)
        {
            return CountEquippedUpgrade(hero, EngineerEquipmentUpgradeCatalog.FieldMedkitUpgradeId);
        }

        public static int CountEquippedFieldMedkits(Agent agent)
        {
            return CountEquippedUpgrade(agent, EngineerEquipmentUpgradeCatalog.FieldMedkitUpgradeId);
        }

        public static int CountEquippedUpgrade(Hero hero, string upgradeId)
        {
            if (hero?.BattleEquipment == null)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                return 0;
            }

            var count = 0;
            foreach (var slot in EquipmentUpgradeSlots)
            {
                var element = hero.BattleEquipment[slot];
                if (!element.IsEmpty &&
                    element.Item != null &&
                    EngineerEquipmentUpgradeCatalog.GetEngineerUpgradeIds(element.Item).Contains(upgradeId))
                {
                    count++;
                }
            }

            return count;
        }

        public static int CountEquippedUpgrade(Agent agent, string upgradeId)
        {
            if (agent?.Equipment == null || string.IsNullOrWhiteSpace(upgradeId))
            {
                return 0;
            }

            var count = 0;
            foreach (var slot in AgentEquipmentUpgradeSlots)
            {
                var element = agent.Equipment[slot];
                if (!element.IsEmpty &&
                    element.Item != null &&
                    EngineerEquipmentUpgradeCatalog.GetEngineerUpgradeIds(element.Item).Contains(upgradeId))
                {
                    count++;
                }
            }

            return count;
        }

        public static int CountEquippedActiveUpgrade(Hero hero, EngineerActiveUpgradeKind activeKind)
        {
            var upgrade = EngineerEquipmentUpgradeCatalog.All.FirstOrDefault(definition => definition.ActiveKind == activeKind);
            return upgrade == null ? 0 : CountEquippedUpgrade(hero, upgrade.Id);
        }

        public static void StoreLastGrenadeUpgrades(Agent agent)
        {
            if (agent == null || agent.WieldedWeapon.IsEmpty)
            {
                return;
            }

            var ammo = agent.WieldedWeapon.AmmoWeapon;
            if (ammo.IsEmpty || ammo.Item == null || !EngineerCareerHelper.IsEngineerGrenadeItem(ammo.Item))
            {
                return;
            }

            var ids = EngineerEquipmentUpgradeCatalog.GetEngineerUpgradeIds(ammo.Item).ToHashSet();
            LastGrenadeUpgradeIdsByAgent[agent.Index] = ids;
        }

        public static bool LastGrenadeHasUpgrade(Agent agent, string upgradeId)
        {
            return agent != null &&
                   LastGrenadeUpgradeIdsByAgent.TryGetValue(agent.Index, out var ids) &&
                   ids.Contains(upgradeId);
        }
    }
}
