using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TOR_Core.AbilitySystem;
using TOR_Core.AbilitySystem.Crosshairs;
using TOR_Core.AbilitySystem.Spells;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerFieldMedkitAbility : Spell
    {
        public const string AbilityId = "EngineerFieldMedkit";
        public const int HealAmount = 50;

        private int _charges;

        public EngineerFieldMedkitAbility(int charges) : base(CreateTemplate(charges))
        {
            _charges = charges;
            RefreshTemplateName();
        }

        public override bool IsDisabled(Agent casterAgent, out TextObject disabledReason)
        {
            if (_charges <= 0)
            {
                disabledReason = new TextObject("{=tor_engineer_medkit_no_charges}No Field Medkit charges left.");
                return true;
            }

            return base.IsDisabled(casterAgent, out disabledReason);
        }

        protected override void DoCast(Agent casterAgent)
        {
            if (_charges <= 0)
            {
                return;
            }

            base.DoCast(casterAgent);
            _charges--;
            RefreshTemplateName();
        }

        public override void ActivateAbility(Agent casterAgent)
        {
            base.ActivateAbility(casterAgent);

            if (casterAgent == null || !casterAgent.IsActive() || casterAgent.Health <= 0f)
            {
                return;
            }

            casterAgent.Heal(HealAmount);
            MBInformationManager.AddQuickInformation(
                new TextObject("{=tor_engineer_medkit_used}Field Medkit restored {HEAL} HP.")
                    .SetTextVariable("HEAL", HealAmount));
        }

        private void RefreshTemplateName()
        {
            Template.Name = "Field Medkit (" + _charges + ")";
        }

        private static AbilityTemplate CreateTemplate(int charges)
        {
            return new AbilityTemplate(AbilityId)
            {
                Name = "Field Medkit (" + charges + ")",
                SpriteName = EngineerCareerHelper.FieldMedkitIconSprite,
                CoolDown = 1,
                WindsOfMagicCost = 0,
                BaseMisCastChance = 0f,
                Duration = 0.05f,
                Radius = 0.1f,
                AbilityType = AbilityType.Spell,
                AbilityEffectType = AbilityEffectType.Heal,
                BaseMovementSpeed = 0f,
                TickInterval = 1f,
                TriggerType = TriggerType.None,
                HasLight = false,
                ParticleEffectPrefab = "none",
                SoundEffectToPlay = "none",
                ShouldSoundLoopOverDuration = false,
                CastType = CastType.Instant,
                CastTime = 0f,
                AnimationActionName = "none",
                AbilityTargetType = AbilityTargetType.Self,
                CrosshairType = CrosshairType.Self,
                MinDistance = 0f,
                MaxDistance = 0f,
                TooltipDescription = "{=tor_engineer_field_medkit_desc}Consumes one equipment charge to heal yourself for 50 HP."
            };
        }
    }

    internal sealed class EngineerEquipmentActiveAbility : Spell
    {
        private const string AbilityIdPrefix = "EngineerUpgrade";

        private readonly EngineerEquipmentUpgradeDefinition _upgrade;
        private int _charges;

        public EngineerEquipmentActiveAbility(EngineerEquipmentUpgradeDefinition upgrade, int charges)
            : base(CreateTemplate(upgrade, charges))
        {
            _upgrade = upgrade;
            _charges = charges;
            RefreshTemplateName();
        }

        public static string GetAbilityId(EngineerActiveUpgradeKind activeKind)
        {
            return AbilityIdPrefix + activeKind;
        }

        public override bool IsDisabled(Agent casterAgent, out TextObject disabledReason)
        {
            if (_charges <= 0)
            {
                disabledReason = new TextObject("{=tor_engineer_upgrade_no_charges}No upgrade charges left.");
                return true;
            }

            if (Mission.Current?.GetMissionBehavior<EngineerEquipmentUpgradeMissionLogic>() == null)
            {
                disabledReason = new TextObject("{=tor_engineer_upgrade_no_logic}Upgrade system is unavailable.");
                return true;
            }

            return base.IsDisabled(casterAgent, out disabledReason);
        }

        protected override void DoCast(Agent casterAgent)
        {
            if (_charges <= 0)
            {
                return;
            }

            base.DoCast(casterAgent);
            _charges--;
            RefreshTemplateName();
        }

        public override void ActivateAbility(Agent casterAgent)
        {
            base.ActivateAbility(casterAgent);

            if (casterAgent == null || !casterAgent.IsActive() || casterAgent.Health <= 0f)
            {
                return;
            }

            var logic = Mission.Current?.GetMissionBehavior<EngineerEquipmentUpgradeMissionLogic>();
            if (logic == null)
            {
                return;
            }

            logic.ActivateUpgrade(_upgrade.ActiveKind, casterAgent, GetTargetPosition(casterAgent));
        }

        private void RefreshTemplateName()
        {
            Template.Name = _upgrade.Name + " (" + _charges + ")";
        }

        private Vec3 GetTargetPosition(Agent casterAgent)
        {
            var position = Crosshair?.Position ?? casterAgent.LookFrame.Advance(GetMaxDistance(_upgrade.ActiveKind) * 0.5f).origin;
            var scene = Mission.Current?.Scene;
            if (scene != null)
            {
                position.z = scene.GetGroundHeightAtPosition(position);
            }

            return position;
        }

        private static AbilityTemplate CreateTemplate(EngineerEquipmentUpgradeDefinition upgrade, int charges)
        {
            var targetGround = TargetsGround(upgrade.ActiveKind);
            var radius = GetRadius(upgrade.ActiveKind);
            return new AbilityTemplate(GetAbilityId(upgrade.ActiveKind))
            {
                Name = upgrade.Name + " (" + charges + ")",
                SpriteName = GetSpriteName(upgrade.ActiveKind),
                CoolDown = 1,
                WindsOfMagicCost = 0,
                BaseMisCastChance = 0f,
                Duration = 0.05f,
                Radius = radius,
                AbilityType = AbilityType.Spell,
                AbilityEffectType = GetEffectType(upgrade.ActiveKind),
                BaseMovementSpeed = 0f,
                TickInterval = 1f,
                TriggerType = TriggerType.None,
                HasLight = false,
                ParticleEffectPrefab = "none",
                SoundEffectToPlay = "none",
                ShouldSoundLoopOverDuration = false,
                CastType = CastType.Instant,
                CastTime = 0f,
                AnimationActionName = "none",
                AbilityTargetType = targetGround ? AbilityTargetType.GroundAtPosition : AbilityTargetType.Self,
                CrosshairType = targetGround ? CrosshairType.TargetedAOE : CrosshairType.Self,
                MinDistance = 0f,
                MaxDistance = targetGround ? GetMaxDistance(upgrade.ActiveKind) : 0f,
                TargetCapturingRadius = radius,
                TooltipDescription = upgrade.Description
            };
        }

        private static bool TargetsGround(EngineerActiveUpgradeKind activeKind)
        {
            return activeKind == EngineerActiveUpgradeKind.EmergencyPowderKeg ||
                   activeKind == EngineerActiveUpgradeKind.TargetingBeacon ||
                   activeKind == EngineerActiveUpgradeKind.GrapnelLauncher;
        }

        private static float GetRadius(EngineerActiveUpgradeKind activeKind)
        {
            return activeKind switch
            {
                EngineerActiveUpgradeKind.EmergencyPowderKeg => EngineerEquipmentUpgradeMissionLogic.PowderKegRadius,
                EngineerActiveUpgradeKind.TargetingBeacon => EngineerEquipmentUpgradeMissionLogic.TargetingBeaconRadius,
                _ => 0.5f
            };
        }

        private static float GetMaxDistance(EngineerActiveUpgradeKind activeKind)
        {
            return activeKind switch
            {
                EngineerActiveUpgradeKind.GrapnelLauncher => EngineerEquipmentUpgradeMissionLogic.GrapnelRange,
                EngineerActiveUpgradeKind.EmergencyPowderKeg => 30f,
                EngineerActiveUpgradeKind.TargetingBeacon => 55f,
                _ => 0f
            };
        }

        private static AbilityEffectType GetEffectType(EngineerActiveUpgradeKind activeKind)
        {
            return activeKind switch
            {
                EngineerActiveUpgradeKind.EmergencyPowderKeg => AbilityEffectType.Blast,
                EngineerActiveUpgradeKind.TargetingBeacon => AbilityEffectType.Hex,
                EngineerActiveUpgradeKind.GrapnelLauncher => AbilityEffectType.TacticalReposition,
                _ => AbilityEffectType.Augment
            };
        }

        private static string GetSpriteName(EngineerActiveUpgradeKind activeKind)
        {
            return activeKind switch
            {
                EngineerActiveUpgradeKind.FieldMedkit => EngineerCareerHelper.FieldMedkitIconSprite,
                EngineerActiveUpgradeKind.EmergencyPowderKeg => EngineerCareerHelper.EmergencyPowderKegIconSprite,
                EngineerActiveUpgradeKind.GalvanicDischarger => EngineerCareerHelper.GalvanicDischargerIconSprite,
                EngineerActiveUpgradeKind.TargetingBeacon => EngineerCareerHelper.TargetingBeaconIconSprite,
                EngineerActiveUpgradeKind.AethericStabilizer => EngineerCareerHelper.AethericStabilizerIconSprite,
                EngineerActiveUpgradeKind.PiercingCalibration => EngineerCareerHelper.PiercingCalibrationIconSprite,
                EngineerActiveUpgradeKind.GrapnelLauncher => EngineerCareerHelper.GrapnelLauncherIconSprite,
                EngineerActiveUpgradeKind.PowderReserve => EngineerCareerHelper.PowderReserveIconSprite,
                EngineerActiveUpgradeKind.RepeaterCrank => EngineerCareerHelper.RepeaterCrankIconSprite,
                _ => EngineerCareerHelper.OpenFireIconSprite
            };
        }
    }

    [HarmonyPatch(typeof(AbilityComponent), MethodType.Constructor, new[] { typeof(Agent) })]
    internal static class EngineerEquipmentUpgradeAbilityComponentPatch
    {
        private static readonly MethodInfo OnCastStartMethod = AccessTools.Method(typeof(AbilityComponent), "OnCastStart");
        private static readonly MethodInfo OnCastCompleteMethod = AccessTools.Method(typeof(AbilityComponent), "OnCastComplete");

        private static void Postfix(AbilityComponent __instance, Agent agent)
        {
            EnsureAbilities(__instance, agent, "AbilityComponent.ctor");
        }

        public static bool EnsureAbilities(AbilityComponent component, Agent agent, string source)
        {
            if (component == null ||
                agent == null ||
                !agent.IsMainAgent)
            {
                return false;
            }

            var hero = agent.GetHero();
            if (!EngineerCareerHelper.IsEngineerHero(hero))
            {
                return false;
            }

            var addedAny = false;
            var heroMedkitCharges = EngineerEquipmentUpgradeHelper.CountEquippedFieldMedkits(hero);
            var agentMedkitCharges = EngineerEquipmentUpgradeHelper.CountEquippedFieldMedkits(agent);
            var charges = Math.Max(heroMedkitCharges, agentMedkitCharges);
            SubModule.Log($"{source}: Field Medkit charges hero={heroMedkitCharges}, agent={agentMedkitCharges}, known={component.KnownAbilitySystem.Count}.");
            if (charges > 0 &&
                component.KnownAbilitySystem.All(ability => ability?.StringID != EngineerFieldMedkitAbility.AbilityId))
            {
                var fieldMedkit = new EngineerFieldMedkitAbility(charges);
                WireAbilityEvents(component, fieldMedkit);
                component.KnownAbilitySystem.Add(fieldMedkit);
                addedAny = true;
                SubModule.Log($"{source}: Added Field Medkit ability with {charges} charge(s). Known abilities: {component.KnownAbilitySystem.Count}.");
            }

            foreach (var upgrade in EngineerEquipmentUpgradeCatalog.All.Where(upgrade =>
                         upgrade.GrantsBattleAbility &&
                         upgrade.ActiveKind != EngineerActiveUpgradeKind.None &&
                         upgrade.ActiveKind != EngineerActiveUpgradeKind.FieldMedkit))
            {
                var abilityId = EngineerEquipmentActiveAbility.GetAbilityId(upgrade.ActiveKind);
                if (component.KnownAbilitySystem.Any(ability => ability?.StringID == abilityId))
                {
                    continue;
                }

                var heroCharges = EngineerEquipmentUpgradeHelper.CountEquippedUpgrade(hero, upgrade.Id);
                var agentCharges = EngineerEquipmentUpgradeHelper.CountEquippedUpgrade(agent, upgrade.Id);
                charges = Math.Max(heroCharges, agentCharges);
                SubModule.Log($"{source}: {upgrade.Id} charges hero={heroCharges}, agent={agentCharges}.");
                if (charges <= 0)
                {
                    continue;
                }

                var ability = new EngineerEquipmentActiveAbility(upgrade, charges);
                WireAbilityEvents(component, ability);
                component.KnownAbilitySystem.Add(ability);
                addedAny = true;
                SubModule.Log($"{source}: Added equipment upgrade ability {upgrade.Id} with {charges} charge(s). Known abilities: {component.KnownAbilitySystem.Count}.");
            }

            return addedAny;
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
}
