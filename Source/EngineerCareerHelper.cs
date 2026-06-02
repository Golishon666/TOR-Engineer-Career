using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TOR_Core.CharacterDevelopment;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal static class EngineerCareerHelper
    {
        public const string CareerIllustrationSprite = "CareerSystem\\Illustrations\\Engineer";
        public const string OpenFireIconSprite = "let_them_have_it_icon";
        public const string ArtilleryBarrageIconSprite = "placeartillery_icon";
        public const string FieldMedkitIconSprite = "engineer_field_medkit_icon";
        public const string EmergencyPowderKegIconSprite = "engineer_emergency_powder_keg_icon";
        public const string GalvanicDischargerIconSprite = "engineer_galvanic_discharger_icon";
        public const string TargetingBeaconIconSprite = "engineer_targeting_beacon_icon";
        public const string AethericStabilizerIconSprite = "engineer_aetheric_stabilizer_icon";
        public const string PiercingCalibrationIconSprite = "engineer_piercing_calibration_icon";
        public const string GrapnelLauncherIconSprite = "engineer_grapnel_launcher_icon";
        public const string PowderReserveIconSprite = "engineer_powder_reserve_icon";
        public const string RepeaterCrankIconSprite = "engineer_repeater_crank_icon";

        public const string GrenadeExplosionId = "grenade_explosion";
        public const string ArtilleryBarrageBurnStatusEffectId = "fireball_dot";
        public const string DefaultArtilleryItemId = "tor_dw_artillery_cannon_001";
        public const int DefaultArtilleryCount = 2;
        public const string IncendiaryFusesKeystone = "IncendiaryFusesKeystone";
        public const string IncendiaryFusesPassive1 = "IncendiaryFusesPassive1";
        public const string IncendiaryFusesPassive2 = "IncendiaryFusesPassive2";
        public const string IncendiaryFusesPassive3 = "IncendiaryFusesPassive3";
        public const string GrandBatteryKeystone = "GrandBatteryKeystone";
        public const string GrandBatteryPassive1 = "GrandBatteryPassive1";
        public const string GrandBatteryPassive2 = "GrandBatteryPassive2";
        public const string GrandBatteryPassive3 = "GrandBatteryPassive3";
        public const string GrandBatteryPassive4 = "GrandBatteryPassive4";
        public const float BaseOpenFireDuration = 15f;
        public const float PowderDrillDurationBonus = 5f;
        public const float RicochetTacticsDurationBonus = 5f;
        public const float AthleticsDurationScale = 0.03f;
        public const float GunPowderEffectScale = 0.001f;
        public const float ThrowingEffectScale = 0.001f;
        public const int GrenadeRefundKillThreshold = 7;
        public const int MaxAbilityCharge = 400;

        private static readonly string[] EngineerKeystoneIds =
        {
            "PowderDrillKeystone",
            "FieldTestingKeystone",
            "ExplosiveRoundsKeystone",
            "SuppressionFireKeystone",
            "GrenadierKeystone",
            "PiercingDoctrineKeystone",
            "RicochetTacticsKeystone",
            IncendiaryFusesKeystone,
            GrandBatteryKeystone
        };

        public static bool IsEngineerHero(Hero hero)
        {
            return hero?.GetCareer()?.StringId == EngineerCareerRegistry.CareerId;
        }

        public static bool IsEngineerMainAgent(Agent agent)
        {
            return agent != null &&
                   agent.IsMainAgent &&
                   IsEngineerHero(Hero.MainHero);
        }

        public static bool HasChoice(string choiceId)
        {
            return Hero.MainHero?.HasCareerChoice(choiceId) == true;
        }

        public static bool HasChoice(Hero hero, string choiceId)
        {
            return hero?.HasCareerChoice(choiceId) == true;
        }

        public static bool IsOpenFireActive(Agent agent)
        {
            return agent?.GetCareerAbility()?.IsActive == true;
        }

        public static int CountEngineerKeystones(Hero hero)
        {
            if (hero == null)
            {
                return 0;
            }

            var choices = hero.GetAllCareerChoices();
            return EngineerKeystoneIds.Count(choices.Contains);
        }

        public static int GetRicochetCount(bool openFireActive)
        {
            var hasKeystone = HasChoice("RicochetTacticsKeystone");
            var hasPassive4 = HasChoice("RicochetTacticsPassive4");

            if (!hasKeystone && !hasPassive4)
            {
                return 0;
            }

            if (openFireActive && hasKeystone)
            {
                return MBMath.ClampInt(CountEngineerKeystones(Hero.MainHero), 1, EngineerKeystoneIds.Length);
            }

            return hasPassive4 ? 1 : 0;
        }

        public static float GetGrenadeRadiusMultiplier()
        {
            return HasChoice("GrenadierPassive2") ? 1.3f : 1f;
        }

        public static float GetGunpowderMissileSpeedMultiplier()
        {
            var multiplier = 1f;
            if (HasChoice("FieldTestingPassive3"))
            {
                multiplier *= 1.15f;
            }

            return multiplier;
        }

        public static float GetGrenadeMissileSpeedMultiplier()
        {
            return HasChoice("GrenadierPassive4") ? 1.3f : 1f;
        }

        public static bool IsEngineerGrenadeItem(ItemObject item)
        {
            return item != null &&
                   (item.IsGrenadeAmmo() ||
                    item.StringId.Contains("blasting_charges"));
        }

        public static void EnsureEngineerGrenadesAreUsable()
        {
            SetItemDifficulty("tor_dw_weapon_grenade_hand_grenade", 0);
            SetItemDifficulty("tor_dw_weapon_blasting_charges", 0);
        }

        public static int GetArtilleryBarrageMaxGunBonus(Hero hero)
        {
            return HasChoice(hero, GrandBatteryKeystone) ? 1 : 0;
        }

        public static int GetArtilleryBarrageShellsPerGunBonus(Hero hero)
        {
            return HasChoice(hero, GrandBatteryKeystone) ? 1 : 0;
        }

        public static int GetArtilleryBarrageFlatShellBonus(Hero hero)
        {
            var bonus = 0;
            if (HasChoice(hero, GrandBatteryPassive1))
            {
                bonus += 2;
            }

            if (HasChoice(hero, GrandBatteryPassive4))
            {
                bonus += 2;
            }

            return bonus;
        }

        public static float GetArtilleryBarrageDamageMultiplier(Hero hero)
        {
            var multiplier = 1f;
            if (HasChoice(hero, IncendiaryFusesPassive1))
            {
                multiplier += 0.10f;
            }

            if (HasChoice(hero, GrandBatteryPassive2))
            {
                multiplier += 0.10f;
            }

            return multiplier;
        }

        public static bool ShouldArtilleryBarrageChargeOpenFire(Hero hero)
        {
            return HasChoice(hero, IncendiaryFusesPassive1);
        }

        public static float GetArtilleryBarrageOpenFireCharge(int damage)
        {
            return damage;
        }

        public static float GetArtilleryBarrageImpactRadiusBonus(Hero hero)
        {
            return HasChoice(hero, GrandBatteryPassive3) ? 0.6f : 0f;
        }

        public static int GetArtilleryBarrageCooldown(Hero hero)
        {
            var cooldown = EngineerArtilleryBarrageAbility.CooldownSeconds;
            if (HasChoice(hero, IncendiaryFusesPassive3))
            {
                cooldown -= 5;
            }

            if (HasChoice(hero, GrandBatteryPassive4))
            {
                cooldown -= 5;
            }

            return MBMath.ClampInt(cooldown, 25, EngineerArtilleryBarrageAbility.CooldownSeconds);
        }

        public static bool ShouldArtilleryBarrageApplyBurn(Hero hero)
        {
            return HasChoice(hero, IncendiaryFusesPassive1);
        }

        public static bool ShouldArtilleryBarrageStrengthenBurn(Hero hero)
        {
            return HasChoice(hero, IncendiaryFusesPassive2);
        }

        public static float GetArtilleryBarrageBurnDuration(Hero hero)
        {
            var duration = 4f;
            if (HasChoice(hero, IncendiaryFusesPassive2))
            {
                duration += 2f;
            }

            return duration;
        }

        public static int GetArtilleryBarrageBurnDamage(Agent caster)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            var engineering = hero?.GetSkillValue(DefaultSkills.Engineering) ?? 0;
            var gunpowderSkill = GetGunpowderSkill();
            var gunpowder = hero == null || gunpowderSkill == null ? 0 : hero.GetSkillValue(gunpowderSkill);
            var damage = 7f + engineering * 0.03f + gunpowder * 0.025f;
            if (HasChoice(hero, IncendiaryFusesPassive2))
            {
                damage *= 1.15f;
            }

            return MBMath.ClampInt((int)damage, 5, 22);
        }

        public static bool ShouldRefundGrenade(Agent triggererAgent, int killCount)
        {
            return killCount >= GrenadeRefundKillThreshold &&
                   IsEngineerMainAgent(triggererAgent) &&
                   HasChoice("GrenadierKeystone");
        }

        public static string BuildOpenFireDurationText()
        {
            return $"Open Fire! lasts {BaseOpenFireDuration:0}s.";
        }

        public static string BuildOpenFireDescriptionText()
        {
            return $"Firearm damage fills Open Fire!, then a {BaseOpenFireDuration:0}s firing order follows.";
        }

        public static SkillObject GetGunpowderSkill()
        {
            return GetSkill("Gunpowder");
        }

        public static void EnsureDefaultArtilleryStock(Hero hero)
        {
            if (!IsEngineerHero(hero))
            {
                return;
            }

            var item = MBObjectManager.Instance?.GetObject<ItemObject>(DefaultArtilleryItemId)
                ?? Game.Current?.ObjectManager?.GetObject<ItemObject>(DefaultArtilleryItemId);
            var roster = hero?.PartyBelongedTo?.ItemRoster
                ?? TaleWorlds.CampaignSystem.Party.PartyBase.MainParty?.ItemRoster;

            if (item == null || roster == null)
            {
                SubModule.Log($"Could not grant default engineer artillery '{DefaultArtilleryItemId}': item or roster missing.");
                return;
            }

            var missingCount = DefaultArtilleryCount - roster.GetItemNumber(item);
            if (missingCount <= 0)
            {
                return;
            }

            roster.AddToCounts(item, missingCount);
            SubModule.Log($"Granted {missingCount} default engineer artillery item(s).");
        }

        public static SkillObject GetSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            if (skillId == "Gunpowder" && TORSkills.Instance != null)
            {
                return TORSkills.GunPowder;
            }

            return MBObjectManager.Instance?.GetObject<SkillObject>(skillId)
                ?? Game.Current?.ObjectManager?.GetObject<SkillObject>(skillId);
        }

        private static void SetItemDifficulty(string itemId, int difficulty)
        {
            var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId)
                ?? Game.Current?.ObjectManager?.GetObject<ItemObject>(itemId);
            if (item == null)
            {
                return;
            }

            AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Difficulty))?.SetValue(item, difficulty);
        }
    }
}
