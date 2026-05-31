using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TOR_Core.AbilitySystem;
using TOR_Core.AbilitySystem.Crosshairs;
using TOR_Core.AbilitySystem.Spells;
using TOR_Core.CharacterDevelopment;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal static class EngineerArtilleryBarrageAbility
    {
        public const string AbilityId = "EngineerArtilleryBarrage";
        public const int CooldownSeconds = 40;
        public const int MaxGuns = 6;
        public const float MinRadius = 8f;
        public const float MaxRadius = 30f;

        private const float MinTargetDistance = 8f;
        private const float MaxTargetDistance = 140f;
        private const int BaseShells = 3;
        private const int ShellsPerGun = 3;
        private const int MinDamage = 45;
        private const int MaxDamage = 160;

        public static bool IsBarrage(Ability ability)
        {
            return ability?.Template?.StringID?.StartsWith(AbilityId, StringComparison.Ordinal) == true;
        }

        public static Ability CreateAbility(Agent agent)
        {
            var guns = GetAvailableGunCount(agent);
            if (guns <= 0)
            {
                return null;
            }

            return new Spell(CreateTemplate(guns));
        }

        public static int GetAvailableGunCount(Agent agent)
        {
            var hero = agent?.GetHero();
            if (hero == null ||
                !EngineerCareerHelper.IsEngineerHero(hero) ||
                !hero.HasAttribute("AbilityUser") ||
                !hero.HasAttribute("CanPlaceArtillery"))
            {
                return 0;
            }

            var party = hero.PartyBelongedTo ?? MobileParty.MainParty;
            var inventoryCount = party?.GetArtilleryItems()?.Sum(item => item.Amount) ?? 0;
            if (inventoryCount <= 0)
            {
                return 0;
            }

            if (party == null)
            {
                return 0;
            }

            var maxGunBonus = EngineerCareerHelper.GetArtilleryBarrageMaxGunBonus(hero);
            var artilleryLimit = Math.Max(1, party.GetMaxNumberOfArtillery() + maxGunBonus);
            var effectiveMaxGuns = GetEffectiveMaxGuns(hero);
            return MBMath.ClampInt(Math.Min(inventoryCount, artilleryLimit), 1, effectiveMaxGuns);
        }

        public static float GetRadius(int guns, Agent caster = null)
        {
            if (guns <= 1)
            {
                return MinRadius;
            }

            var hero = caster?.GetHero() ?? Hero.MainHero;
            var effectiveMaxGuns = GetEffectiveMaxGuns(hero);
            var progress = (float)(guns - 1) / (effectiveMaxGuns - 1);
            return MBMath.ClampFloat(MinRadius + (MaxRadius - MinRadius) * progress, MinRadius, MaxRadius);
        }

        public static int GetShellCount(int guns, Agent caster = null)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            var effectiveMaxGuns = GetEffectiveMaxGuns(hero);
            var clampedGuns = MBMath.ClampInt(guns, 1, effectiveMaxGuns);
            var shellsPerGun = ShellsPerGun + EngineerCareerHelper.GetArtilleryBarrageShellsPerGunBonus(hero);
            return BaseShells + EngineerCareerHelper.GetArtilleryBarrageFlatShellBonus(hero) + clampedGuns * shellsPerGun;
        }

        public static int GetImpactDamage(Agent caster)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            if (hero == null)
            {
                return MinDamage;
            }

            var engineering = hero.GetSkillValue(DefaultSkills.Engineering);
            var gunpowderSkill = EngineerCareerHelper.GetGunpowderSkill();
            var gunpowder = gunpowderSkill == null ? 0 : hero.GetSkillValue(gunpowderSkill);
            var damage = (MinDamage + engineering * 0.25f + gunpowder * 0.20f) * EngineerCareerHelper.GetArtilleryBarrageDamageMultiplier(hero);
            return MBMath.ClampInt((int)damage, MinDamage, MaxDamage);
        }

        public static int GetEffectiveMaxGuns(Hero hero)
        {
            return MaxGuns + EngineerCareerHelper.GetArtilleryBarrageMaxGunBonus(hero);
        }

        public static bool CanUse(Agent caster, out TextObject disabledReason, out int guns)
        {
            guns = GetAvailableGunCount(caster);
            if (guns <= 0)
            {
                disabledReason = new TextObject("{=tor_engineer_barrage_no_artillery}Requires at least one deployable artillery piece.");
                return false;
            }

            if (caster == null || !caster.IsActive() || caster.Health <= 0f)
            {
                disabledReason = new TextObject("{=tor_engineer_barrage_caster_unavailable}Caster is unavailable.");
                return false;
            }

            disabledReason = new TextObject("");
            return true;
        }

        private static AbilityTemplate CreateTemplate(int guns)
        {
            var radius = GetRadius(guns);
            var hero = Hero.MainHero;
            return new AbilityTemplate(AbilityId)
            {
                Name = "{=tor_engineer_artillery_barrage}Artillery Barrage",
                SpriteName = EngineerCareerHelper.ArtilleryBarrageIconSprite,
                CoolDown = EngineerCareerHelper.GetArtilleryBarrageCooldown(hero),
                WindsOfMagicCost = 0,
                BaseMisCastChance = 0f,
                Duration = 0.5f,
                Radius = 1f,
                AbilityType = AbilityType.Spell,
                AbilityEffectType = AbilityEffectType.Bombardment,
                BaseMovementSpeed = 0f,
                TickInterval = 0.1f,
                TriggerType = TriggerType.TickOnce,
                HasLight = false,
                LightIntensity = 0f,
                LightRadius = 0f,
                ShadowCastEnabled = false,
                ParticleEffectPrefab = "none",
                ParticleEffectSizeModifier = 0f,
                SoundEffectToPlay = "none",
                ShouldSoundLoopOverDuration = false,
                CastType = CastType.WindUp,
                CastTime = 0.6f,
                AnimationActionName = "act_release_heavy_thrown",
                Offset = 80f,
                AbilityTargetType = AbilityTargetType.GroundAtPosition,
                CrosshairType = CrosshairType.TargetedAOE,
                MinDistance = MinTargetDistance,
                MaxDistance = MaxTargetDistance,
                TargetCapturingRadius = radius,
                TooltipDescription = "{=tor_engineer_artillery_barrage_desc}Call in an inaccurate artillery barrage. Requires deployable artillery in the party. More artillery increases the strike radius and number of shells. Incendiary Fuses and Grand Battery career branches improve the barrage."
            };
        }
    }

    [HarmonyPatch(typeof(AbilityComponent), MethodType.Constructor, new[] { typeof(Agent) })]
    internal static class EngineerArtilleryBarrageAbilityComponentPatch
    {
        private static readonly MethodInfo OnCastStartMethod = AccessTools.Method(typeof(AbilityComponent), "OnCastStart");
        private static readonly MethodInfo OnCastCompleteMethod = AccessTools.Method(typeof(AbilityComponent), "OnCastComplete");

        private static void Postfix(AbilityComponent __instance, Agent agent)
        {
            if (__instance == null || agent == null || __instance.KnownAbilitySystem.Any(EngineerArtilleryBarrageAbility.IsBarrage))
            {
                return;
            }

            var ability = EngineerArtilleryBarrageAbility.CreateAbility(agent);
            if (ability == null)
            {
                return;
            }

            WireAbilityEvents(__instance, ability);
            __instance.KnownAbilitySystem.Add(ability);
        }

        private static void WireAbilityEvents(AbilityComponent component, Ability ability)
        {
            if (OnCastStartMethod != null)
            {
                var handler = (Ability.OnCastStartHandler)Delegate.CreateDelegate(typeof(Ability.OnCastStartHandler), component, OnCastStartMethod);
                ability.OnCastStart += handler;
            }

            if (OnCastCompleteMethod != null)
            {
                var handler = (Ability.OnCastCompleteHandler)Delegate.CreateDelegate(typeof(Ability.OnCastCompleteHandler), component, OnCastCompleteMethod);
                ability.OnCastComplete += handler;
            }
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.IsDisabled))]
    internal static class EngineerArtilleryBarrageDisabledPatch
    {
        private static void Postfix(Ability __instance, Agent casterAgent, ref bool __result, ref TextObject disabledReason)
        {
            if (!EngineerArtilleryBarrageAbility.IsBarrage(__instance) || __result)
            {
                return;
            }

            if (!EngineerArtilleryBarrageAbility.CanUse(casterAgent, out disabledReason, out _))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.CanCast))]
    internal static class EngineerArtilleryBarrageCanCastPatch
    {
        private static bool Prefix(Ability __instance, Agent casterAgent, ref bool __result, ref TextObject failureReason)
        {
            if (!EngineerArtilleryBarrageAbility.IsBarrage(__instance))
            {
                return true;
            }

            if (EngineerArtilleryBarrageAbility.CanUse(casterAgent, out failureReason, out _))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.ActivateAbility))]
    internal static class EngineerArtilleryBarrageActivatePatch
    {
        private static void Postfix(Ability __instance, Agent casterAgent)
        {
            if (!EngineerArtilleryBarrageAbility.IsBarrage(__instance) ||
                !EngineerArtilleryBarrageAbility.CanUse(casterAgent, out _, out var guns))
            {
                return;
            }

            var target = __instance.Crosshair?.Position ?? casterAgent.LookFrame.Advance(25f).origin;
            var logic = Mission.Current?.GetMissionBehavior<EngineerArtilleryBarrageMissionLogic>();
            logic?.QueueBarrage(casterAgent, target, guns);
        }
    }
}
