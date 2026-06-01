using System.Collections.Generic;
using System.Linq;
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

                var upgradeLogic = Mission.Current?.GetMissionBehavior<EngineerEquipmentUpgradeMissionLogic>();
                if (upgradeLogic?.HasAethericStabilizer(agent) == true)
                {
                    agentDrivenProperties.MissileSpeedMultiplier *= 1.20f;
                    agentDrivenProperties.ReloadSpeed *= 1.15f;
                    agentDrivenProperties.WeaponInaccuracy *= 0.65f;
                }
            }

            if (!weapon.IsEmpty && weapon.IsAnyAmmo() && EngineerCareerHelper.IsEngineerGrenadeItem(weapon.Item))
            {
                agentDrivenProperties.MissileSpeedMultiplier *= EngineerCareerHelper.GetGrenadeMissileSpeedMultiplier();
            }
        }
    }

    [HarmonyPatch(typeof(TORAgentStatCalculateModel), nameof(TORAgentStatCalculateModel.UpdateAgentStats))]
    internal static class EngineerGrenadeAmmoPatch
    {
        private static readonly Dictionary<int, HashSet<int>> InitializedGrenadeSlotsByAgent = new();

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
                    !EngineerCareerHelper.IsEngineerGrenadeItem(missionWeapon.Item))
                {
                    continue;
                }

                if (!TryMarkInitialized(agent.Index, i))
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

        public static void Reset()
        {
            InitializedGrenadeSlotsByAgent.Clear();
        }

        private static bool TryMarkInitialized(int agentIndex, int slotIndex)
        {
            if (!InitializedGrenadeSlotsByAgent.TryGetValue(agentIndex, out var slots))
            {
                slots = [];
                InitializedGrenadeSlotsByAgent[agentIndex] = slots;
            }

            if (slots.Contains(slotIndex))
            {
                return false;
            }

            slots.Add(slotIndex);
            return true;
        }
    }

    [HarmonyPatch(typeof(FirearmsMissionLogic), nameof(FirearmsMissionLogic.OnAgentShootMissile))]
    internal static class EngineerBuckshotPatch
    {
        private static void Prefix(Agent shooterAgent, ref float velocity)
        {
            EngineerEquipmentUpgradeHelper.StoreLastGrenadeUpgrades(shooterAgent);

            if (!EngineerCareerHelper.IsEngineerMainAgent(shooterAgent))
            {
                return;
            }

            var ammo = shooterAgent.WieldedWeapon.AmmoWeapon;
            if (!ammo.IsEmpty && EngineerCareerHelper.IsEngineerGrenadeItem(ammo.Item))
            {
                velocity *= EngineerCareerHelper.GetGrenadeMissileSpeedMultiplier();
            }

            var upgradeLogic = Mission.Current?.GetMissionBehavior<EngineerEquipmentUpgradeMissionLogic>();
            var weapon = shooterAgent.WieldedWeapon;
            if (!weapon.IsEmpty &&
                weapon.CurrentUsageItem != null &&
                weapon.CurrentUsageItem.IsGunPowderWeapon() &&
                upgradeLogic?.HasAethericStabilizer(shooterAgent) == true)
            {
                velocity *= 1.20f;
            }
        }

        private static void Postfix(Agent shooterAgent)
        {
            if (!EngineerCareerHelper.IsEngineerMainAgent(shooterAgent))
            {
                return;
            }

            var weaponData = shooterAgent.WieldedWeapon.CurrentUsageItem;
            var isGunpowderShot = weaponData != null && weaponData.IsGunPowderWeapon();
            var upgradeLogic = Mission.Current?.GetMissionBehavior<EngineerEquipmentUpgradeMissionLogic>();
            if (isGunpowderShot)
            {
                upgradeLogic?.ConsumeAethericShot(shooterAgent);
                if (upgradeLogic?.TryConsumeRepeaterCrank(shooterAgent) == true)
                {
                    FireRepeaterCrank(shooterAgent, weaponData, upgradeLogic.GetRepeaterExtraShotCount(shooterAgent));
                }
            }

            if (!EngineerCareerHelper.HasChoice("RicochetTacticsPassive4"))
            {
                return;
            }

            var weaponItemId = shooterAgent.WieldedWeapon.Item?.StringId;
            if (!IsBuckshotWeapon(weaponItemId))
            {
                return;
            }

            var ammoId = shooterAgent.WieldedWeapon.AmmoWeapon.Item?.StringId;
            if (string.IsNullOrEmpty(ammoId) ||
                (!ammoId.Contains("scatter") && !ammoId.Contains("buckshot")))
            {
                return;
            }

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

        private static bool IsBuckshotWeapon(string weaponItemId)
        {
            return !string.IsNullOrEmpty(weaponItemId) &&
                   (weaponItemId.IndexOf("blunderbuss", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    weaponItemId.IndexOf("grudge_raker", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void FireRepeaterCrank(Agent shooterAgent, WeaponComponentData weaponData, int extraShotCount)
        {
            if (extraShotCount <= 0)
            {
                return;
            }

            var ammo = shooterAgent.WieldedWeapon.AmmoWeapon;
            if (ammo.IsEmpty)
            {
                return;
            }

            var logic = Mission.Current?.GetMissionBehavior<FirearmsMissionLogic>();
            if (logic == null)
            {
                return;
            }

            var frame = shooterAgent.Frame;
            var accuracy = 1f / (weaponData.Accuracy * 1.05f);
            logic.ScatterShot(
                shooterAgent,
                accuracy,
                ammo,
                frame.origin,
                frame.rotation,
                weaponData.MissileSpeed,
                (short)extraShotCount);
        }
    }

    [HarmonyPatch(typeof(TORAgentApplyDamageModel), nameof(TORAgentApplyDamageModel.ApplyGeneralDamageModifiers))]
    internal static class EngineerEquipmentUpgradeDamagePatch
    {
        private static void Postfix(in AttackInformation attackInformation, in AttackCollisionData collisionData, ref float __result)
        {
            var logic = Mission.Current?.GetMissionBehavior<EngineerEquipmentUpgradeMissionLogic>();
            if (logic == null)
            {
                return;
            }

            var attacker = attackInformation.AttackerAgent;
            var victim = attackInformation.VictimAgent;
            if (attacker == null || victim == null)
            {
                return;
            }

            if (logic.IsInsideTargetingBeacon(victim, attacker))
            {
                __result *= 1.20f;
            }

            if (IsGunpowderAttack(attackInformation, collisionData) &&
                logic.TryConsumePiercingCalibration(attacker))
            {
                __result *= 1.35f;
            }
        }

        private static bool IsGunpowderAttack(in AttackInformation attackInformation, in AttackCollisionData collisionData)
        {
            if (!collisionData.IsMissile)
            {
                return false;
            }

            var weapon = attackInformation.AttackerWeapon;
            return !weapon.IsEmpty &&
                   weapon.CurrentUsageItem != null &&
                   weapon.CurrentUsageItem.IsGunPowderWeapon();
        }
    }

    [HarmonyPatch(typeof(TriggeredEffect), nameof(TriggeredEffect.Trigger))]
    internal static class EngineerGrenadeExplosionPatch
    {
        private const float BaseGrenadeExplosionRadius = 5f;
        private static readonly Dictionary<TriggeredEffect, GrenadeExplosionState> PendingExplosions = new();
        private static readonly Dictionary<int, int> ExplosiveKillProgressByAgentIndex = new();

        private static void Prefix(TriggeredEffect __instance, Vec3 position, Agent triggererAgent)
        {
            var isEngineerMainAgent = EngineerCareerHelper.IsEngineerMainAgent(triggererAgent);
            var hasFragmentationCasing = EngineerEquipmentUpgradeHelper.LastGrenadeHasUpgrade(triggererAgent, "eng_upgrade_grenade_fragmentation_casing");
            var hasShapedCharge = EngineerEquipmentUpgradeHelper.LastGrenadeHasUpgrade(triggererAgent, "eng_upgrade_grenade_shaped_charge");

            if (!isEngineerMainAgent && !hasFragmentationCasing && !hasShapedCharge)
            {
                return;
            }

            var template = Traverse.Create(__instance).Field<TriggeredEffectTemplate>("_template").Value;
            if (template?.StringID?.StartsWith(EngineerCareerHelper.GrenadeExplosionId) != true)
            {
                return;
            }

            var radius = BaseGrenadeExplosionRadius * (isEngineerMainAgent ? EngineerCareerHelper.GetGrenadeRadiusMultiplier() : 1f);
            if (hasFragmentationCasing)
            {
                radius *= 1.15f;
            }

            var state = new GrenadeExplosionState(triggererAgent, radius, template);
            if (isEngineerMainAgent)
            {
                foreach (var agent in Mission.Current.GetNearbyAgents(position.AsVec2, radius, new MBList<Agent>()))
                {
                    if (agent != null && agent.IsHuman && agent.IsEnemyOf(triggererAgent) && agent.IsActive() && agent.Health > 0f)
                    {
                        state.HealthSnapshot[agent] = agent.Health;
                    }
                }
            }

            PendingExplosions[__instance] = state;

            if (!hasFragmentationCasing && !hasShapedCharge && !EngineerCareerHelper.HasChoice("GrenadierPassive2"))
            {
                return;
            }

            var runtimeTemplate = (TriggeredEffectTemplate)template.Clone(EngineerCareerHelper.GrenadeExplosionId + "_engineer");
            runtimeTemplate.Radius = radius;
            if (hasShapedCharge)
            {
                runtimeTemplate.DamageAmount = (int)(runtimeTemplate.DamageAmount * 1.15f);
            }

            Traverse.Create(__instance).Field<TriggeredEffectTemplate>("_template").Value = runtimeTemplate;
        }

        private static void Postfix(TriggeredEffect __instance, Agent triggererAgent)
        {
            if (!PendingExplosions.TryGetValue(__instance, out var state))
            {
                return;
            }

            PendingExplosions.Remove(__instance);
            Traverse.Create(__instance).Field<TriggeredEffectTemplate>("_template").Value = state.OriginalTemplate;
            if (!EngineerCareerHelper.IsEngineerMainAgent(triggererAgent))
            {
                return;
            }

            RegisterExplosionKills(triggererAgent, state.HealthSnapshot.Count(entry => !entry.Key.IsActive() || entry.Key.Health <= 0f));
        }

        internal static void RegisterExplosionKills(Agent triggererAgent, int killCount)
        {
            if (killCount <= 0 || !EngineerCareerHelper.IsEngineerMainAgent(triggererAgent) || !EngineerCareerHelper.HasChoice("GrenadierKeystone"))
            {
                return;
            }

            var totalKills = ExplosiveKillProgressByAgentIndex.TryGetValue(triggererAgent.Index, out var progress)
                ? progress + killCount
                : killCount;

            while (totalKills >= EngineerCareerHelper.GrenadeRefundKillThreshold)
            {
                if (!RefundGrenade(triggererAgent))
                {
                    break;
                }

                totalKills -= EngineerCareerHelper.GrenadeRefundKillThreshold;
            }

            ExplosiveKillProgressByAgentIndex[triggererAgent.Index] = totalKills;
        }

        public static void Reset()
        {
            PendingExplosions.Clear();
            ExplosiveKillProgressByAgentIndex.Clear();
        }

        private static bool RefundGrenade(Agent agent)
        {
            for (var i = 0; i < 5; i++)
            {
                var weapon = agent.Equipment[(EquipmentIndex)i];
                if (weapon.IsEmpty || !EngineerCareerHelper.IsEngineerGrenadeItem(weapon.Item))
                {
                    continue;
                }

                agent.SetWeaponAmountInSlot((EquipmentIndex)i, (short)(weapon.Amount + 1), true);
                return true;
            }

            return false;
        }

        private sealed class GrenadeExplosionState
        {
            public GrenadeExplosionState(Agent caster, float radius, TriggeredEffectTemplate originalTemplate)
            {
                Caster = caster;
                Radius = radius;
                OriginalTemplate = originalTemplate;
                HealthSnapshot = new Dictionary<Agent, float>();
            }

            public Agent Caster { get; }
            public float Radius { get; }
            public TriggeredEffectTemplate OriginalTemplate { get; }
            public Dictionary<Agent, float> HealthSnapshot { get; }
        }
    }

}
