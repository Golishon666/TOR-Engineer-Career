using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerArtilleryBarrageMissionLogic : MissionLogic
    {
        private const float FirstImpactDelay = 0.7f;
        private const float ImpactInterval = 0.18f;
        private const float ImpactRadius = 4.2f;
        private const float DamageVariance = 0.18f;
        private const string ImpactParticle = "psys_fireball_explosion_1";
        private const string ImpactSound = "mortar_explosion_1";

        private readonly List<ScheduledImpact> _scheduledImpacts = new();

        public void QueueBarrage(Agent caster, Vec3 targetPosition, int guns)
        {
            if (caster == null || Mission.Current == null)
            {
                return;
            }

            var shellCount = EngineerArtilleryBarrageAbility.GetShellCount(guns);
            var radius = EngineerArtilleryBarrageAbility.GetRadius(guns);
            var damage = EngineerArtilleryBarrageAbility.GetImpactDamage(caster);
            var startTime = Mission.Current.CurrentTime + FirstImpactDelay;

            targetPosition.z = Mission.Current.Scene.GetGroundHeightAtPosition(targetPosition);
            PlayIncomingSound(targetPosition);

            for (var i = 0; i < shellCount; i++)
            {
                var impactPosition = GetRandomImpactPosition(targetPosition, radius);
                _scheduledImpacts.Add(new ScheduledImpact(
                    startTime + i * ImpactInterval + MBRandom.RandomFloatRanged(0f, 0.12f),
                    caster,
                    impactPosition,
                    damage));
            }
        }

        public override void OnMissionTick(float dt)
        {
            if (_scheduledImpacts.Count == 0 || Mission.Current == null)
            {
                return;
            }

            var currentTime = Mission.Current.CurrentTime;
            for (var i = _scheduledImpacts.Count - 1; i >= 0; i--)
            {
                var impact = _scheduledImpacts[i];
                if (impact.ImpactTime > currentTime)
                {
                    continue;
                }

                TriggerImpact(impact);
                _scheduledImpacts.RemoveAt(i);
            }
        }

        public override void OnClearScene()
        {
            _scheduledImpacts.Clear();
            base.OnClearScene();
        }

        private static Vec3 GetRandomImpactPosition(Vec3 center, float radius)
        {
            var angle = MBRandom.RandomFloatRanged(0f, (float)(System.Math.PI * 2.0));
            var distance = (float)System.Math.Sqrt(MBRandom.RandomFloat) * radius;
            var position = center + new Vec3((float)System.Math.Cos(angle) * distance, (float)System.Math.Sin(angle) * distance, 0f);
            position.z = Mission.Current.Scene.GetGroundHeightAtPosition(position);
            return position;
        }

        private static void TriggerImpact(ScheduledImpact impact)
        {
            PlayImpactFeedback(impact.Position);

            var caster = impact.Caster;
            if (caster == null || Mission.Current == null)
            {
                return;
            }

            var targets = Mission.Current
                .GetNearbyAgents(impact.Position.AsVec2, ImpactRadius, new MBList<Agent>())
                .Where(agent => IsValidTarget(agent, caster))
                .ToList();

            foreach (var target in targets)
            {
                var distance = target.Position.Distance(impact.Position);
                var falloff = MBMath.ClampFloat(1f - distance / ImpactRadius, 0.35f, 1f);
                var variance = MBRandom.RandomFloatRanged(1f - DamageVariance, 1f + DamageVariance);
                var damage = MBMath.ClampInt((int)(impact.Damage * falloff * variance), 1, impact.Damage);
                target.ApplyDamage(damage, impact.Position, caster, doBlow: true, hasShockWave: true, originatesFromAbility: true);
            }
        }

        private static bool IsValidTarget(Agent agent, Agent caster)
        {
            return agent != null &&
                   agent.IsHuman &&
                   agent.IsActive() &&
                   agent.Health > 0f &&
                   !agent.IsFadingOut() &&
                   agent.IsEnemyOf(caster);
        }

        private static void PlayIncomingSound(Vec3 position)
        {
            var soundIndex = SoundEvent.GetEventIdFromString("mortar_fire");
            if (soundIndex >= 0)
            {
                Mission.Current.MakeSound(soundIndex, position, false, false, -1, -1);
            }
        }

        private static void PlayImpactFeedback(Vec3 position)
        {
            var frame = MatrixFrame.Identity;
            frame.origin = position;
            Mission.Current.AddParticleSystemBurstByName(ImpactParticle, frame, false);

            var soundIndex = SoundEvent.GetEventIdFromString(ImpactSound);
            if (soundIndex >= 0)
            {
                Mission.Current.MakeSound(soundIndex, position, false, false, -1, -1);
            }
        }

        private readonly struct ScheduledImpact
        {
            public ScheduledImpact(float impactTime, Agent caster, Vec3 position, int damage)
            {
                ImpactTime = impactTime;
                Caster = caster;
                Position = position;
                Damage = damage;
            }

            public float ImpactTime { get; }
            public Agent Caster { get; }
            public Vec3 Position { get; }
            public int Damage { get; }
        }
    }
}
