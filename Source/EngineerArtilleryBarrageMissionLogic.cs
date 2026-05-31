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
        private const float MaxRandomImpactDelay = 5f;
        private const float ShellFlightTime = 1.4f;
        private const float ShellSpawnHeightMin = 48f;
        private const float ShellSpawnHeightMax = 70f;
        private const float ShellHorizontalDriftMin = 8f;
        private const float ShellHorizontalDriftMax = 18f;
        private const float ImpactRadius = 4.2f;
        private const float DamageVariance = 0.18f;
        private const float ShellVisualScale = 1.15f;
        private const string ShellMeshName = "cannonball_001";
        private const string ImpactParticle = "psys_fireball_explosion_1";
        private const string ImpactSound = "mortar_explosion_1";
        private const string SalvoSoundOne = "mortar_shot_1";
        private const string SalvoSoundTwo = "mortar_shot_2";

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
            var currentTime = Mission.Current.CurrentTime;

            targetPosition.z = Mission.Current.Scene.GetGroundHeightAtPosition(targetPosition);
            PlaySalvoSound(caster.Position);

            for (var i = 0; i < shellCount; i++)
            {
                var impactPosition = GetRandomImpactPosition(targetPosition, radius);
                var impactTime = currentTime + FirstImpactDelay + MBRandom.RandomFloatRanged(0f, MaxRandomImpactDelay);
                var visualStartTime = MBMath.ClampFloat(impactTime - ShellFlightTime, currentTime, impactTime);
                _scheduledImpacts.Add(new ScheduledImpact(
                    impactTime,
                    visualStartTime,
                    caster,
                    GetShellStartPosition(impactPosition),
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
                UpdateShellVisual(impact, currentTime);
                if (impact.ImpactTime > currentTime)
                {
                    continue;
                }

                RemoveShellVisual(impact);
                TriggerImpact(impact);
                _scheduledImpacts.RemoveAt(i);
            }
        }

        public override void OnClearScene()
        {
            foreach (var impact in _scheduledImpacts)
            {
                RemoveShellVisual(impact);
            }

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

        private static Vec3 GetShellStartPosition(Vec3 impactPosition)
        {
            var angle = MBRandom.RandomFloatRanged(0f, (float)(System.Math.PI * 2.0));
            var distance = MBRandom.RandomFloatRanged(ShellHorizontalDriftMin, ShellHorizontalDriftMax);
            return new Vec3(
                impactPosition.x + (float)System.Math.Cos(angle) * distance,
                impactPosition.y + (float)System.Math.Sin(angle) * distance,
                impactPosition.z + MBRandom.RandomFloatRanged(ShellSpawnHeightMin, ShellSpawnHeightMax));
        }

        private static void UpdateShellVisual(ScheduledImpact impact, float currentTime)
        {
            if (currentTime < impact.VisualStartTime || Mission.Current == null)
            {
                return;
            }

            if (impact.ProjectileEntity == null)
            {
                impact.ProjectileEntity = CreateShellVisual(impact.StartPosition);
                if (impact.ProjectileEntity == null)
                {
                    return;
                }
            }

            var duration = System.Math.Max(0.01f, impact.ImpactTime - impact.VisualStartTime);
            var progress = MBMath.ClampFloat((currentTime - impact.VisualStartTime) / duration, 0f, 1f);
            var easedProgress = progress * progress * (3f - 2f * progress);
            var position = impact.StartPosition + (impact.Position - impact.StartPosition) * easedProgress;

            var frame = MatrixFrame.Identity;
            frame.origin = position;
            frame.rotation.RotateAboutSide(progress * 18f);
            frame.Scale(new Vec3(ShellVisualScale, ShellVisualScale, ShellVisualScale));
            impact.ProjectileEntity.SetGlobalFrame(frame);
        }

        private static GameEntity CreateShellVisual(Vec3 position)
        {
            var scene = Mission.Current?.Scene;
            if (scene == null)
            {
                return null;
            }

            var shell = GameEntity.CreateEmpty(scene);
            var mesh = MetaMesh.GetCopy(ShellMeshName);
            if (mesh == null)
            {
                shell.Remove(0);
                return null;
            }

            shell.AddMultiMesh(mesh, true);

            var frame = MatrixFrame.Identity;
            frame.origin = position;
            frame.Scale(new Vec3(ShellVisualScale, ShellVisualScale, ShellVisualScale));
            shell.SetGlobalFrame(frame);
            return shell;
        }

        private static void RemoveShellVisual(ScheduledImpact impact)
        {
            if (impact.ProjectileEntity == null)
            {
                return;
            }

            impact.ProjectileEntity.FadeOut(0.05f, true);
            impact.ProjectileEntity = null;
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

        private static void PlaySalvoSound(Vec3 position)
        {
            PlaySound(position, SalvoSoundOne);
            PlaySound(position, SalvoSoundTwo);
        }

        private static void PlaySound(Vec3 position, string soundName)
        {
            var soundIndex = SoundEvent.GetEventIdFromString(soundName);
            if (soundIndex >= 0 && Mission.Current != null)
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

        private sealed class ScheduledImpact
        {
            public ScheduledImpact(float impactTime, float visualStartTime, Agent caster, Vec3 startPosition, Vec3 position, int damage)
            {
                ImpactTime = impactTime;
                VisualStartTime = visualStartTime;
                Caster = caster;
                StartPosition = startPosition;
                Position = position;
                Damage = damage;
            }

            public float ImpactTime { get; }
            public float VisualStartTime { get; }
            public Agent Caster { get; }
            public Vec3 StartPosition { get; }
            public Vec3 Position { get; }
            public int Damage { get; }
            public GameEntity ProjectileEntity { get; set; }
        }
    }
}
