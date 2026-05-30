using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TOR_Core.Extensions;
using TOR_Core.Utilities;

namespace TOR_EngineerCareer
{
    internal static class EngineerDwarfContrabandCatalog
    {
        public const int PrestigeCostPerTier = 500;
        public const int MaxTier = 3;

        private static readonly string[] Tier1ItemIds =
        {
            "tor_dw_weapon_ammo_musket_ball",
            "tor_dw_weapon_gun_beardling_handgun",
            "tor_dw_weapon_blasting_charges",
            "tor_dwarf_1h_spanner_001",
            "tor_dwarf_2h_spanner_001",
            "tor_dw_weapon_crossbow_001",
            "tor_dw_weapon_crossbow_002",
            "tor_dw_weapon_gun_handgun_001",
            "tor_dw_weapon_gun_handgun_002",
            "tor_dw_head_apprentice_002",
            "tor_dw_head_apprentice_001",
            "tor_dw_head_journeyman_001",
            "tor_dw_shoulder_shoulderpads_apprentice_001",
            "tor_dw_body_armour_apprentice_001",
            "tor_dw_body_armour_journeyman_001",
            "tor_dw_body_armour_engineer_001",
            "tor_dw_arm_gloves_apprentice_001",
            "tor_dw_arm_gloves_journeyman_001",
            "tor_dw_arm_gloves_engineer_001",
            "tor_dw_leg_boots_apprentice_001",
            "tor_dw_leg_boots_journeyman_001",
            "tor_dw_leg_boots_engineer_001"
        };

        private static readonly string[] Tier2ExtraItemIds =
        {
            "tor_dw_artillery_cannon_001",
            "tor_dw_weapon_gun_drakefire_pistol",
            "tor_dw_weapon_grenade_hand_grenade",
            "dwarf_1h_engineer_hammer_001",
            "dwarf_2h_engineer_hammer_001",
            "tor_dw_gun_grudge_raker_001",
            "tor_dw_weapon_ammo_buckshot",
            "tor_dw_weapon_crossbow_003",
            "tor_dw_weapon_crossbow_004",
            "tor_dw_weapon_gun_handgun_003",
            "tor_dw_head_engineer_002",
            "tor_dw_shoulder_shoulderpads_journeyman_001"
        };

        private static readonly string[] Tier3ExtraItemIds =
        {
            "tor_dw_gun_dronazgrund",
            "tor_dw_weapon_gun_handgun_004",
            "tor_dw_weapon_gun_trollhammer",
            "tor_dw_iron_drake_trollhammer_torpedo",
            "tor_dw_head_engineer_001",
            "tor_dw_shoulder_shoulderpads_engineer_001"
        };

        public static string GetUnlockAttributeId(int tier) => "EngineerDwarfContraband" + tier;

        public static bool HasTierUnlocked(Hero hero, int tier)
        {
            return hero != null && tier >= 1 && tier <= MaxTier && hero.HasAttribute(GetUnlockAttributeId(tier));
        }

        public static int GetUnlockedTierCount(Hero hero)
        {
            var count = 0;
            for (var tier = 1; tier <= MaxTier; tier++)
            {
                if (HasTierUnlocked(hero, tier))
                {
                    count = tier;
                }
            }

            return count;
        }

        public static bool CanOfferContraband(Hero hero)
        {
            return EngineerCareerHelper.IsEngineerHero(hero) &&
                   hero.HasAttribute("CanPlaceArtillery");
        }

        public static bool HasEnoughPrestige(Hero hero)
        {
            return hero != null && hero.GetCustomResourceValue("Prestige") >= PrestigeCostPerTier;
        }

        public static void UnlockTier(Hero hero, int tier)
        {
            if (hero == null || tier < 1 || tier > MaxTier)
            {
                return;
            }

            var attributeId = GetUnlockAttributeId(tier);
            if (!hero.HasAttribute(attributeId))
            {
                hero.AddAttribute(attributeId);
            }

            hero.AddCustomResource("Prestige", -PrestigeCostPerTier);
        }

        public static void AppendUnlockedItemsToRoster(ItemRoster roster, Hero hero)
        {
            if (roster == null || !CanOfferContraband(hero))
            {
                return;
            }

            var unlockedTier = GetUnlockedTierCount(hero);
            if (unlockedTier <= 0)
            {
                return;
            }

            foreach (var item in GetItemsForTier(unlockedTier))
            {
                roster.Add(new ItemRosterElement(item, MBRandom.RandomInt(1, 3)));
            }
        }

        private static IEnumerable<ItemObject> GetItemsForTier(int unlockedTier)
        {
            var items = new List<ItemObject>();
            AddItemsById(items, Tier1ItemIds);

            if (unlockedTier >= 2)
            {
                AddGunpowderWeapons(items);
                AddItemsById(items, Tier2ExtraItemIds);
            }

            if (unlockedTier >= 3)
            {
                AddItemsById(items, Tier3ExtraItemIds);
            }

            return items
                .Where(x => x != null)
                .GroupBy(x => x.StringId)
                .Select(x => x.First());
        }

        private static void AddGunpowderWeapons(List<ItemObject> items)
        {
            foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (item.Culture?.StringId != TORConstants.Cultures.DAWI ||
                    !item.IsTorItem() ||
                    !item.IsGunPowderWeapon() ||
                    item.IsFlameThrowerItem())
                {
                    continue;
                }

                items.Add(item);
            }
        }

        private static void AddItemsById(List<ItemObject> items, IEnumerable<string> itemIds)
        {
            foreach (var itemId in itemIds)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null || item.IsFlameThrowerItem())
                {
                    continue;
                }

                items.Add(item);
            }
        }
    }
}
