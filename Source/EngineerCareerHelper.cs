using System.Collections.Generic;
using System.Linq;
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
        public const string OpenFireIconSprite = "engineer_open_fire_icon";

        public const string GrenadeExplosionId = "grenade_explosion";
        public const string DefaultArtilleryItemId = "tor_dw_artillery_cannon_001";
        public const int DefaultArtilleryCount = 2;
        public const float BaseOpenFireDuration = 15f;
        public const float PowderDrillDurationBonus = 5f;
        public const float RicochetTacticsDurationBonus = 5f;
        public const float AthleticsDurationScale = 0.03f;
        public const float GunPowderEffectScale = 0.001f;
        public const float ThrowingEffectScale = 0.001f;
        public const int GrenadeRefundKillThreshold = 15;
        public const int MaxAbilityCharge = 400;

        private static readonly string[] EngineerKeystoneIds =
        {
            "PowderDrillKeystone",
            "FieldTestingKeystone",
            "ExplosiveRoundsKeystone",
            "SuppressionFireKeystone",
            "GrenadierKeystone",
            "PiercingDoctrineKeystone",
            "RicochetTacticsKeystone"
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

        public static bool ShouldRefundGrenade(Agent triggererAgent, int killCount)
        {
            return killCount >= GrenadeRefundKillThreshold &&
                   IsEngineerMainAgent(triggererAgent) &&
                   HasChoice("GrenadierKeystone") &&
                   IsOpenFireActive(triggererAgent);
        }

        public static string BuildOpenFireDurationText()
        {
            return $"Base duration: {BaseOpenFireDuration:0}s. Powder Drill and Ricochet Tactics keystones add +{PowderDrillDurationBonus:0}s each. Field Testing keystone adds +{AthleticsDurationScale:0.##}s per Athletics level. Grenadier keystone adds +{ThrowingEffectScale * 100:0.#}% Open Fire! power per Throwing level (100 Throwing = +10%).";
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
    }
}
