using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TOR_Core.BattleMechanics.DamageSystem;
using TOR_Core.Extensions;
using TOR_Core.Extensions.ExtendedInfoSystem;
using TOR_Core.Items;

namespace TOR_EngineerCareer
{
    internal enum EngineerEquipmentUpgradeCategory
    {
        Armor,
        Firearm,
        Bullet,
        Grenade
    }

    internal enum EngineerActiveUpgradeKind
    {
        None,
        FieldMedkit,
        EmergencyPowderKeg,
        GalvanicDischarger,
        TargetingBeacon,
        AethericStabilizer,
        PiercingCalibration,
        GrapnelLauncher,
        PowderReserve,
        RepeaterCrank
    }

    internal sealed class EngineerEquipmentUpgradeDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public EngineerEquipmentUpgradeCategory Category { get; set; }
        public int Tier { get; set; }
        public int GoldCost => EngineerEquipmentUpgradeCatalog.GetGoldCostForTier(Tier);
        public ItemObject MetalItem => EngineerEquipmentUpgradeCatalog.GetMetalItemForTier(Tier);
        public int MetalCost => EngineerEquipmentUpgradeCatalog.GetMetalCostForTier(Tier);
        public TorTradeGoodType IngredientType { get; set; }
        public int IngredientCost => Tier;
        public ItemTraitStatType StatType { get; set; } = ItemTraitStatType.Invalid;
        public float StatValue { get; set; }
        public DamageType ResistanceType { get; set; } = DamageType.Invalid;
        public float ResistanceValue { get; set; }
        public string ImbuedStatusEffectId { get; set; } = "none";
        public float ImbuedEffectChance { get; set; } = 0.25f;
        public bool GrantsBattleAbility { get; set; }
        public EngineerActiveUpgradeKind ActiveKind { get; set; } = EngineerActiveUpgradeKind.None;
    }

    internal static class EngineerEquipmentUpgradeCatalog
    {
        public const string UpgradeIdPrefix = "eng_upgrade_";
        public const string UpgradeTraitIconName = "traits_fire_icon";
        public const string FieldMedkitUpgradeId = "eng_upgrade_armor_field_medkit";
        public const string GalvanicDischargerUpgradeId = "eng_upgrade_armor_galvanic_discharger";
        public const string GrapnelLauncherUpgradeId = "eng_upgrade_armor_grapnel_launcher";
        public const string TargetingBeaconUpgradeId = "eng_upgrade_firearm_targeting_beacon";
        public const string AethericStabilizerUpgradeId = "eng_upgrade_firearm_aetheric_stabilizer";
        public const string PiercingCalibrationUpgradeId = "eng_upgrade_firearm_piercing_calibration";
        public const string RepeaterCrankUpgradeId = "eng_upgrade_firearm_repeater_crank";
        public const string PowderReserveUpgradeId = "eng_upgrade_bullet_powder_reserve";
        public const string EmergencyPowderKegUpgradeId = "eng_upgrade_grenade_emergency_powder_keg";

        private static readonly List<EngineerEquipmentUpgradeDefinition> _upgrades =
        [
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_armor_reinforced_rivets",
                Name = "Reinforced Rivets",
                Description = "+4 hit points.",
                Category = EngineerEquipmentUpgradeCategory.Armor,
                Tier = 1,
                IngredientType = TorTradeGoodType.BlessedWater,
                StatType = ItemTraitStatType.HealthMax,
                StatValue = 4f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = FieldMedkitUpgradeId,
                Name = "Field Medkit",
                Description = "+1 Field Medkit charge: heal self for 50 HP.",
                Category = EngineerEquipmentUpgradeCategory.Armor,
                Tier = 1,
                IngredientType = TorTradeGoodType.BlessedWater,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.FieldMedkit
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_armor_blast_padding",
                Name = "Blast Padding",
                Description = "+30% fire resistance.",
                Category = EngineerEquipmentUpgradeCategory.Armor,
                Tier = 2,
                IngredientType = TorTradeGoodType.AmberCrystal,
                ResistanceType = DamageType.Fire,
                ResistanceValue = 0.30f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = GalvanicDischargerUpgradeId,
                Name = "Galvanic Discharger",
                Description = "15s shock aura; damages nearby enemies.",
                Category = EngineerEquipmentUpgradeCategory.Armor,
                Tier = 2,
                IngredientType = TorTradeGoodType.AmberCrystal,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.GalvanicDischarger
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = GrapnelLauncherUpgradeId,
                Name = "Grapnel Launcher",
                Description = "Pull to target point.",
                Category = EngineerEquipmentUpgradeCategory.Armor,
                Tier = 2,
                IngredientType = TorTradeGoodType.GemStone,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.GrapnelLauncher
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_firearm_rifled_barrel",
                Name = "Rifled Barrel",
                Description = "+12% missile speed.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 1,
                IngredientType = TorTradeGoodType.GemStone,
                StatType = ItemTraitStatType.MissileSpeed,
                StatValue = 12f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_firearm_clockwork_lock",
                Name = "Clockwork Lock",
                Description = "+12% reload speed.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 2,
                IngredientType = TorTradeGoodType.ArcaneScroll,
                StatType = ItemTraitStatType.ReloadSpeed,
                StatValue = 12f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = AethericStabilizerUpgradeId,
                Name = "Aetheric Stabilizer",
                Description = "Buff next 3 shots.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 2,
                IngredientType = TorTradeGoodType.ArcaneScroll,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.AethericStabilizer
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_firearm_telescopic_sight",
                Name = "Telescopic Sight",
                Description = "+15% armor penetration.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 3,
                IngredientType = TorTradeGoodType.GemStone,
                StatType = ItemTraitStatType.ArmorPenetration,
                StatValue = 15f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = TargetingBeaconUpgradeId,
                Name = "Targeting Beacon",
                Description = "Mark area for +20% damage.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 3,
                IngredientType = TorTradeGoodType.GemStone,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.TargetingBeacon
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = PiercingCalibrationUpgradeId,
                Name = "Piercing Calibration",
                Description = "Next shot pierces armor.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 3,
                IngredientType = TorTradeGoodType.GemStone,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.PiercingCalibration
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = RepeaterCrankUpgradeId,
                Name = "Repeater Crank",
                Description = "Next shot fires extras.",
                Category = EngineerEquipmentUpgradeCategory.Firearm,
                Tier = 3,
                IngredientType = TorTradeGoodType.GemStone,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.RepeaterCrank
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_bullet_hardened_shot",
                Name = "Hardened Shot",
                Description = "+12% armor penetration.",
                Category = EngineerEquipmentUpgradeCategory.Bullet,
                Tier = 1,
                IngredientType = TorTradeGoodType.GemStone,
                StatType = ItemTraitStatType.ArmorPenetration,
                StatValue = 12f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_bullet_sealed_cartridges",
                Name = "Sealed Cartridges",
                Description = "+10% missile speed.",
                Category = EngineerEquipmentUpgradeCategory.Bullet,
                Tier = 1,
                IngredientType = TorTradeGoodType.ArcaneScroll,
                StatType = ItemTraitStatType.MissileSpeed,
                StatValue = 10f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = PowderReserveUpgradeId,
                Name = "Powder Reserve",
                Description = "Restore carried ammo.",
                Category = EngineerEquipmentUpgradeCategory.Bullet,
                Tier = 1,
                IngredientType = TorTradeGoodType.ArcaneScroll,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.PowderReserve
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_bullet_incendiary_charge",
                Name = "Incendiary Charge",
                Description = "20% chance to ignite on hit.",
                Category = EngineerEquipmentUpgradeCategory.Bullet,
                Tier = 2,
                IngredientType = TorTradeGoodType.DragonBlood,
                ImbuedStatusEffectId = EngineerCareerHelper.ArtilleryBarrageBurnStatusEffectId,
                ImbuedEffectChance = 0.20f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_grenade_stabilizing_fins",
                Name = "Stabilizing Fins",
                Description = "+15% grenade speed.",
                Category = EngineerEquipmentUpgradeCategory.Grenade,
                Tier = 1,
                IngredientType = TorTradeGoodType.GemStone,
                StatType = ItemTraitStatType.MissileSpeed,
                StatValue = 15f
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = EmergencyPowderKegUpgradeId,
                Name = "Emergency Powder Keg",
                Description = "Place a delayed explosive.",
                Category = EngineerEquipmentUpgradeCategory.Grenade,
                Tier = 3,
                IngredientType = TorTradeGoodType.DragonBlood,
                GrantsBattleAbility = true,
                ActiveKind = EngineerActiveUpgradeKind.EmergencyPowderKeg
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_grenade_fragmentation_casing",
                Name = "Fragmentation Casing",
                Description = "+15% explosion radius.",
                Category = EngineerEquipmentUpgradeCategory.Grenade,
                Tier = 2,
                IngredientType = TorTradeGoodType.DragonBlood
            },
            new EngineerEquipmentUpgradeDefinition
            {
                Id = "eng_upgrade_grenade_shaped_charge",
                Name = "Shaped Charge",
                Description = "+15% explosion damage.",
                Category = EngineerEquipmentUpgradeCategory.Grenade,
                Tier = 3,
                IngredientType = TorTradeGoodType.WarpstoneDust
            }
        ];

        public static IReadOnlyList<EngineerEquipmentUpgradeDefinition> All => _upgrades;

        public static EngineerEquipmentUpgradeDefinition Get(string id)
        {
            return _upgrades.FirstOrDefault(upgrade => upgrade.Id == id);
        }

        public static bool IsEngineerUpgradeId(string traitId)
        {
            return !string.IsNullOrWhiteSpace(traitId) &&
                   traitId.StartsWith(UpgradeIdPrefix, StringComparison.Ordinal);
        }

        public static IEnumerable<string> GetEngineerUpgradeIds(ItemObject item)
        {
            var traits = item?.GetTorSpecificDataReadOnly()?.ItemTraits;
            if (traits == null)
            {
                return Enumerable.Empty<string>();
            }

            return traits.Where(IsEngineerUpgradeId);
        }

        public static int CountEngineerUpgrades(ItemObject item)
        {
            return GetEngineerUpgradeIds(item).Count();
        }

        public static int GetMaxUpgradeSlots(Hero hero)
        {
            var engineering = hero?.GetSkillValue(DefaultSkills.Engineering) ?? 0;
            return MBMath.ClampInt(engineering / 100, 0, 3);
        }

        public static bool IsValidForItem(EngineerEquipmentUpgradeDefinition upgrade, ItemObject item)
        {
            if (upgrade == null || item == null)
            {
                return false;
            }

            return upgrade.Category switch
            {
                EngineerEquipmentUpgradeCategory.Armor => item.IsArmor(),
                EngineerEquipmentUpgradeCategory.Firearm => IsFirearm(item),
                EngineerEquipmentUpgradeCategory.Bullet => IsBullet(item),
                EngineerEquipmentUpgradeCategory.Grenade => EngineerCareerHelper.IsEngineerGrenadeItem(item),
                _ => false
            };
        }

        public static bool ItemCanReceiveAnyKnownUpgrade(ItemObject item, IEnumerable<string> knownUpgradeIds, int maxSlots)
        {
            if (item == null || maxSlots <= 0 || CountEngineerUpgrades(item) >= maxSlots)
            {
                return false;
            }

            var currentIds = GetEngineerUpgradeIds(item).ToHashSet();
            return knownUpgradeIds
                .Select(Get)
                .Any(upgrade => upgrade != null &&
                                !currentIds.Contains(upgrade.Id) &&
                                IsValidForItem(upgrade, item));
        }

        public static ItemObject GetMetalItemForTier(int tier)
        {
            return tier switch
            {
                <= 1 => DefaultItems.IronIngot4,
                2 => DefaultItems.IronIngot5,
                _ => DefaultItems.IronIngot6
            };
        }

        public static int GetGoldCostForTier(int tier)
        {
            return tier switch
            {
                <= 1 => 3000,
                2 => 7500,
                _ => 20000
            };
        }

        public static int GetMetalCostForTier(int tier)
        {
            return tier switch
            {
                <= 1 => 5,
                2 => 10,
                _ => 20
            };
        }

        public static string GetCategoryText(EngineerEquipmentUpgradeCategory category)
        {
            return category switch
            {
                EngineerEquipmentUpgradeCategory.Armor => "Armor",
                EngineerEquipmentUpgradeCategory.Firearm => "Firearm",
                EngineerEquipmentUpgradeCategory.Bullet => "Bullets",
                EngineerEquipmentUpgradeCategory.Grenade => "Grenades",
                _ => category.ToString()
            };
        }

        private static bool IsFirearm(ItemObject item)
        {
            return item.IsGunPowderWeapon() &&
                   (item.ItemType == ItemObject.ItemTypeEnum.Musket ||
                    item.ItemType == ItemObject.ItemTypeEnum.Pistol);
        }

        private static bool IsBullet(ItemObject item)
        {
            return item.IsGunPowderWeapon() &&
                   !EngineerCareerHelper.IsEngineerGrenadeItem(item) &&
                   item.WeaponComponent?.PrimaryWeapon != null &&
                   item.WeaponComponent.PrimaryWeapon.WeaponClass == WeaponClass.Cartridge;
        }

        public static ItemTrait CreateItemTrait(EngineerEquipmentUpgradeDefinition upgrade)
        {
            var trait = (ItemTrait)Activator.CreateInstance(typeof(ItemTrait), true);
            trait.ItemTraitStringId = upgrade.Id;
            trait.ItemTraitName = upgrade.Name;
            trait.ItemTraitDescription = upgrade.Description;
            trait.IsCraftable = false;
            trait.ValidItemType = ToTraitItemType(upgrade.Category);
            trait.IngredientItem = TorTradeGoodType.Invalid;
            trait.IngredientAmount = 0;
            trait.IconName = UpgradeTraitIconName;
            trait.ImbuedStatusEffectId = upgrade.ImbuedStatusEffectId;
            trait.ImbuedEffectChance = upgrade.ImbuedEffectChance;

            if (upgrade.StatType != ItemTraitStatType.Invalid)
            {
                trait.StatsTuple = new StatsTuple
                {
                    StatType = upgrade.StatType,
                    SkillId = "none",
                    Value = upgrade.StatValue
                };
            }

            if (upgrade.ResistanceType != DamageType.Invalid && upgrade.ResistanceValue > 0f)
            {
                trait.ResistanceTuple = new ResistanceTuple
                {
                    ResistedDamageType = upgrade.ResistanceType,
                    ReductionPercent = upgrade.ResistanceValue
                };
            }

            return trait;
        }

        private static ItemTraitItemType ToTraitItemType(EngineerEquipmentUpgradeCategory category)
        {
            return category switch
            {
                EngineerEquipmentUpgradeCategory.Armor => ItemTraitItemType.Armor,
                EngineerEquipmentUpgradeCategory.Firearm => ItemTraitItemType.Ranged,
                EngineerEquipmentUpgradeCategory.Bullet => ItemTraitItemType.Ammo,
                EngineerEquipmentUpgradeCategory.Grenade => ItemTraitItemType.Ammo,
                _ => ItemTraitItemType.Invalid
            };
        }
    }
}
