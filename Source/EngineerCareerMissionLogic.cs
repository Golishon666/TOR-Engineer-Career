using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.BattleMechanics.DamageSystem;
using TOR_Core.Extensions;
using TOR_Core.Utilities;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerCareerMissionLogic : MissionLogic
    {
        private const float ExplosiveRadius = 3f;
        private const int ExplosiveDamage = 60;
        private const float RicochetExplosionRadius = 2.5f;
        private const int RicochetExplosionDamage = 45;
        private const float RicochetDetonationChance = 0.5f;
        private const float OverpenetrationRange = 7f;
        private const float OverpenetrationConeDot = 0.86f;
        private const float RicochetRange = 6f;

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            if (!CanUseEngineerEffect(affectedAgent, affectorAgent, affectorWeapon, blow))
            {
                return;
            }

            var choices = Hero.MainHero.GetAllCareerChoices();

            if (choices.Contains("ExplosiveRoundsKeystone"))
            {
                ApplyExplosiveRound(affectedAgent, affectorAgent);
            }

            if (choices.Contains("PiercingDoctrineKeystone"))
            {
                ApplyOverpenetration(affectedAgent, affectorAgent, blow, attackCollisionData);
            }

            ApplyRicochets(affectedAgent, affectorAgent, blow);
        }

        private static bool CanUseEngineerEffect(Agent affectedAgent, Agent affectorAgent, MissionWeapon affectorWeapon, Blow blow)
        {
            if (Mission.Current == null ||
                affectedAgent == null ||
                affectorAgent == null ||
                affectedAgent == affectorAgent ||
                !affectorAgent.IsMainAgent ||
                Hero.MainHero?.GetCareer()?.StringId != EngineerCareerRegistry.CareerId ||
                blow.InflictedDamage <= 0 ||
                affectorWeapon.IsEmpty ||
                affectorWeapon.CurrentUsageItem == null ||
                !affectorWeapon.CurrentUsageItem.IsGunPowderWeapon())
            {
                return false;
            }

            return affectedAgent.IsHuman && affectedAgent.IsActive() && affectedAgent.IsEnemyOf(affectorAgent);
        }

        private static void ApplyRicochets(Agent affectedAgent, Agent affectorAgent, Blow blow)
        {
            var ricochetCount = EngineerCareerHelper.GetRicochetCount(EngineerCareerHelper.IsOpenFireActive(affectorAgent));
            if (ricochetCount <= 0)
            {
                return;
            }

            var excluded = new HashSet<Agent> { affectedAgent };
            for (var i = 0; i < ricochetCount; i++)
            {
                var secondary = FindRicochetTarget(affectedAgent, affectorAgent, excluded);
                if (secondary == null)
                {
                    break;
                }

                excluded.Add(secondary);
                var damage = MBMath.ClampInt((int)(blow.InflictedDamage * 0.4f), 1, 90);
                TORMissionHelper.DamageAgents(new[] { secondary }, damage, damage, affectorAgent, damageType: DamageType.Physical, hasShockWave: false, impactPosition: secondary.Position, originSpellTemplate: affectorAgent.GetCareerAbility()?.Template);

                var ricochetRadius = EngineerCareerHelper.IsOpenFireActive(affectorAgent)
                    ? RicochetExplosionRadius + 0.5f
                    : RicochetExplosionRadius;
                if (MBRandom.RandomFloat <= RicochetDetonationChance)
                {
                    ApplyExplosionAt(secondary.Position, affectorAgent, ricochetRadius, RicochetExplosionDamage);
                }
            }
        }

        private static Agent FindRicochetTarget(Agent originAgent, Agent affectorAgent, HashSet<Agent> excluded)
        {
            return Mission.Current
                .GetNearbyAgents(originAgent.Position.AsVec2, RicochetRange, new MBList<Agent>())
                .Where(agent => IsValidSecondaryTarget(agent, affectorAgent) && !excluded.Contains(agent))
                .OrderBy(agent => agent.Position.DistanceSquared(originAgent.Position))
                .FirstOrDefault();
        }

        private static void ApplyExplosiveRound(Agent affectedAgent, Agent affectorAgent)
        {
            var radius = EngineerCareerHelper.IsOpenFireActive(affectorAgent) ? ExplosiveRadius + 1f : ExplosiveRadius;
            ApplyExplosionAt(affectedAgent.Position, affectorAgent, radius, ExplosiveDamage);
        }

        private static void ApplyExplosionAt(Vec3 position, Agent affectorAgent, float radius, int damage)
        {
            PlayExplosionFeedback(position);

            var targets = Mission.Current
                .GetNearbyAgents(position.AsVec2, radius, new MBList<Agent>())
                .Where(agent => IsValidSecondaryTarget(agent, affectorAgent))
                .ToList();

            if (targets.Count == 0)
            {
                return;
            }

            TORMissionHelper.DamageAgents(targets, damage, damage, affectorAgent, damageType: DamageType.Fire, hasShockWave: false, impactPosition: position, originSpellTemplate: affectorAgent.GetCareerAbility()?.Template);
        }

        private static void ApplyOverpenetration(Agent affectedAgent, Agent affectorAgent, Blow blow, AttackCollisionData attackCollisionData)
        {
            var direction = attackCollisionData.WeaponBlowDir;
            if (direction.LengthSquared < 0.01f)
            {
                direction = blow.Direction;
            }

            if (direction.LengthSquared < 0.01f)
            {
                direction = affectorAgent.LookDirection;
            }

            direction.Normalize();
            var origin = affectedAgent.Position + direction * 0.4f;
            var secondary = Mission.Current
                .GetNearbyAgents(affectedAgent.Position.AsVec2, OverpenetrationRange, new MBList<Agent>())
                .Where(agent => IsValidSecondaryTarget(agent, affectorAgent) && agent != affectedAgent)
                .Select(agent => new { Agent = agent, Offset = agent.Position - origin })
                .Where(x => x.Offset.Length <= OverpenetrationRange && Vec3.DotProduct(x.Offset.NormalizedCopy(), direction) >= OverpenetrationConeDot)
                .OrderBy(x => x.Offset.Length)
                .Select(x => x.Agent)
                .FirstOrDefault();

            if (secondary == null)
            {
                return;
            }

            var damage = MBMath.ClampInt((int)(blow.InflictedDamage * 0.6f), 1, 120);
            TORMissionHelper.DamageAgents(new[] { secondary }, damage, damage, affectorAgent, damageType: DamageType.Physical, hasShockWave: false, impactPosition: affectedAgent.Position, originSpellTemplate: affectorAgent.GetCareerAbility()?.Template);
        }

        private static bool IsValidSecondaryTarget(Agent agent, Agent affectorAgent)
        {
            return agent != null &&
                   agent.IsHuman &&
                   agent.IsActive() &&
                   agent.Health > 0f &&
                   !agent.IsFadingOut() &&
                   agent.IsEnemyOf(affectorAgent);
        }

        private static void PlayExplosionFeedback(Vec3 position)
        {
            var frame = MatrixFrame.Identity;
            frame.origin = position;
            Mission.Current.AddParticleSystemBurstByName("psys_fireball_explosion_1", frame, false);

            var soundIndex = TaleWorlds.Engine.SoundEvent.GetEventIdFromString("mortar_explosion_1");
            if (soundIndex >= 0)
            {
                Mission.Current.MakeSound(soundIndex, position, false, false, -1, -1);
            }
        }
    }
}
