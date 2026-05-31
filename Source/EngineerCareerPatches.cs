using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.BattleMechanics.Firearms;
using TOR_Core.BattleMechanics.TriggeredEffect;
using TOR_Core.CampaignMechanics.Choices;
using TOR_Core.CharacterDevelopment;
using TOR_Core.Extensions;
using TOR_Core.Models;

namespace TOR_EngineerCareer
{
    [HarmonyPatch(typeof(RangedSiegeWeapon), nameof(RangedSiegeWeapon.Shoot))]
    internal static class EngineerArtilleryShootPatch
    {
        private static bool Prefix(RangedSiegeWeapon __instance, ref bool __result)
        {
            if (!EngineerArtilleryControlMissionLogic.ShouldBlockShoot(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class EngineerArtilleryProjectileDirectionPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.AllTypes()
                .Where(type => type != null && typeof(RangedSiegeWeapon).IsAssignableFrom(type))
                .Select(type => AccessTools.Method(type, "SetupProjectileToShoot"))
                .Where(method => method != null)
                .Cast<MethodBase>()
                .Distinct();
        }

        private static void Postfix(
            RangedSiegeWeapon __instance,
            ref Vec3 direction,
            ref Mat3 orientation,
            ref float missileBaseSpeed,
            ref float missileShootingSpeed)
        {
            if (!EngineerArtilleryControlMissionLogic.TryGetManualShotTarget(__instance, out var target))
            {
                return;
            }

            try
            {
                var origin = EngineerArtilleryControlMissionLogic.GetWeaponOrigin(__instance);
                var currentSpeed = MathF.Max(missileBaseSpeed, missileShootingSpeed);
                if (!EngineerArtilleryControlMissionLogic.TryGetManualBallisticShot(__instance, origin, target, currentSpeed, out direction, out var manualSpeed, out _))
                {
                    return;
                }

                direction.Normalize();
                orientation = Mat3.CreateMat3WithForward(in direction);

                missileBaseSpeed = MathF.Max(missileBaseSpeed, manualSpeed);
                missileShootingSpeed = MathF.Max(missileShootingSpeed, manualSpeed);
            }
            finally
            {
            }
        }
    }

    [HarmonyPatch]
    internal static class EngineerArtilleryAddMissilePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Mission), "AddMissileAux");
            yield return AccessTools.Method(typeof(Mission), "AddMissileSingleUsageAux");
        }

        private static void Prefix(
            ref Vec3 position,
            ref Vec3 direction,
            ref Mat3 orientation,
            ref float baseSpeed,
            ref float speed)
        {
            if (!EngineerArtilleryControlMissionLogic.TryGetAnyManualShotTarget(out var weapon, out var target))
            {
                return;
            }

            try
            {
                var currentSpeed = MathF.Max(baseSpeed, speed);
                if (!EngineerArtilleryControlMissionLogic.TryGetManualBallisticShot(weapon, position, target, currentSpeed, out direction, out var manualSpeed, out _))
                {
                    return;
                }

                direction.Normalize();
                orientation = Mat3.CreateMat3WithForward(in direction);
                baseSpeed = MathF.Max(baseSpeed, manualSpeed);
                speed = MathF.Max(speed, manualSpeed);
            }
            finally
            {
                EngineerArtilleryControlMissionLogic.ClearManualShotTarget(weapon);
            }
        }
    }

    [HarmonyPatch(typeof(TORAgentApplyDamageModel), nameof(TORAgentApplyDamageModel.DecideAgentShrugOffBlow))]
    internal static class EngineerRangedStaggerImmunityPatch
    {
        private static void Postfix(Agent victimAgent, in AttackCollisionData collisionData, ref bool __result)
        {
            if (__result ||
                !collisionData.IsMissile ||
                !EngineerCareerHelper.IsEngineerMainAgent(victimAgent) ||
                !EngineerCareerHelper.HasChoice("ExplosiveRoundsPassive2"))
            {
                return;
            }

            __result = true;
        }
    }

    [HarmonyPatch(typeof(TORAgentStatCalculateModel), nameof(TORAgentStatCalculateModel.UpdateAgentStats))]
    internal static class EngineerAgentStatsPatch
    {
        private static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            if (!EngineerCareerHelper.IsEngineerMainAgent(agent))
            {
                return;
            }

            var weapon = agent.WieldedWeapon;
            if (!weapon.IsEmpty &&
                weapon.CurrentUsageItem != null &&
                weapon.CurrentUsageItem.IsGunPowderWeapon())
            {
                agentDrivenProperties.MissileSpeedMultiplier *= EngineerCareerHelper.GetGunpowderMissileSpeedMultiplier();
            }

            if (!weapon.IsEmpty && weapon.IsAnyAmmo() && weapon.Item.IsGrenadeAmmo())
            {
                agentDrivenProperties.MissileSpeedMultiplier *= EngineerCareerHelper.GetGrenadeMissileSpeedMultiplier();
            }
        }
    }

    [HarmonyPatch(typeof(TORAgentStatCalculateModel), nameof(TORAgentStatCalculateModel.UpdateAgentStats))]
    internal static class EngineerGrenadeAmmoPatch
    {
        private static void Postfix(Agent agent)
        {
            if (!EngineerCareerHelper.IsEngineerMainAgent(agent) || !EngineerCareerHelper.HasChoice("GrenadierPassive1"))
            {
                return;
            }

            var choice = TORCareerChoices.GetChoice("GrenadierPassive1");
            if (choice == null)
            {
                return;
            }

            var bonus = (short)choice.GetPassiveValue();
            var equipment = agent.Equipment;

            for (var i = 0; i < 5; i++)
            {
                var index = (EquipmentIndex)i;
                var missionWeapon = equipment[index];
                if (missionWeapon.IsEmpty ||
                    missionWeapon.CurrentUsageItem == null ||
                    !missionWeapon.Item.IsSpecialAmmunitionItem())
                {
                    continue;
                }

                var baseAmount = missionWeapon.Item?.PrimaryWeapon?.MaxDataValue ?? missionWeapon.Amount;
                var targetAmount = (short)(baseAmount + bonus);
                if (missionWeapon.Amount != targetAmount)
                {
                    agent.SetWeaponAmountInSlot(index, targetAmount, true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(FirearmsMissionLogic), nameof(FirearmsMissionLogic.OnAgentShootMissile))]
    internal static class EngineerBuckshotPatch
    {
        private static void Prefix(Agent shooterAgent, ref float velocity)
        {
            if (!EngineerCareerHelper.IsEngineerMainAgent(shooterAgent))
            {
                return;
            }

            var ammo = shooterAgent.WieldedWeapon.AmmoWeapon;
            if (!ammo.IsEmpty && ammo.Item.IsGrenadeAmmo())
            {
                velocity *= EngineerCareerHelper.GetGrenadeMissileSpeedMultiplier();
            }
        }

        private static void Postfix(Agent shooterAgent)
        {
            if (!EngineerCareerHelper.IsEngineerMainAgent(shooterAgent) ||
                !EngineerCareerHelper.HasChoice("RicochetTacticsPassive4"))
            {
                return;
            }

            var ammoId = shooterAgent.WieldedWeapon.AmmoWeapon.Item?.StringId;
            if (string.IsNullOrEmpty(ammoId) ||
                (!ammoId.Contains("scatter") && !ammoId.Contains("buckshot")))
            {
                return;
            }

            var weaponData = shooterAgent.WieldedWeapon.CurrentUsageItem;
            if (weaponData == null)
            {
                return;
            }

            var logic = Mission.Current?.GetMissionBehavior<FirearmsMissionLogic>();
            if (logic == null)
            {
                return;
            }

            var frame = shooterAgent.Frame;
            var orientation = frame.rotation;
            var position = frame.origin;
            var accuracy = 1f / (weaponData.Accuracy * 1.2f);
            logic.ScatterShot(
                shooterAgent,
                accuracy,
                shooterAgent.WieldedWeapon.AmmoWeapon,
                position,
                orientation,
                weaponData.MissileSpeed,
                3);
        }
    }

    [HarmonyPatch(typeof(TriggeredEffect), nameof(TriggeredEffect.Trigger))]
    internal static class EngineerGrenadeExplosionPatch
    {
        private const float BaseGrenadeExplosionRadius = 5f;
        private static readonly Dictionary<Agent, float> HealthSnapshot = new();

        private static void Prefix(TriggeredEffect __instance, Vec3 position, Agent triggererAgent)
        {
            HealthSnapshot.Clear();

            if (!EngineerCareerHelper.IsEngineerMainAgent(triggererAgent))
            {
                return;
            }

            var template = Traverse.Create(__instance).Field<TriggeredEffectTemplate>("_template").Value;
            if (template?.StringID != EngineerCareerHelper.GrenadeExplosionId)
            {
                return;
            }

            var radius = BaseGrenadeExplosionRadius * EngineerCareerHelper.GetGrenadeRadiusMultiplier();
            foreach (var agent in Mission.Current.GetNearbyAgents(position.AsVec2, radius, new MBList<Agent>()))
            {
                if (agent != null && agent.IsHuman && agent.IsEnemyOf(triggererAgent) && agent.IsActive() && agent.Health > 0f)
                {
                    HealthSnapshot[agent] = agent.Health;
                }
            }

            if (!EngineerCareerHelper.HasChoice("GrenadierPassive2"))
            {
                return;
            }

            var runtimeTemplate = (TriggeredEffectTemplate)template.Clone(EngineerCareerHelper.GrenadeExplosionId + "_engineer");
            runtimeTemplate.Radius = radius;
            Traverse.Create(__instance).Field<TriggeredEffectTemplate>("_template").Value = runtimeTemplate;
        }

        private static void Postfix(TriggeredEffect __instance, Agent triggererAgent)
        {
            if (!EngineerCareerHelper.IsEngineerMainAgent(triggererAgent))
            {
                return;
            }

            var template = Traverse.Create(__instance).Field<TriggeredEffectTemplate>("_template").Value;
            if (template?.StringID?.StartsWith(EngineerCareerHelper.GrenadeExplosionId) != true)
            {
                return;
            }

            var killCount = HealthSnapshot.Count(entry => !entry.Key.IsActive() || entry.Key.Health <= 0f);
            if (EngineerCareerHelper.ShouldRefundGrenade(triggererAgent, killCount))
            {
                RefundGrenade(triggererAgent);
            }

            HealthSnapshot.Clear();
        }

        private static void RefundGrenade(Agent agent)
        {
            for (var i = 0; i < 5; i++)
            {
                var weapon = agent.Equipment[(EquipmentIndex)i];
                if (weapon.IsEmpty || !weapon.Item.IsGrenadeAmmo())
                {
                    continue;
                }

                agent.SetWeaponAmountInSlot((EquipmentIndex)i, (short)(weapon.Amount + 1), true);
                break;
            }
        }
    }
}
