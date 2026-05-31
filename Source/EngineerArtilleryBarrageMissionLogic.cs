using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.BattleMechanics.DamageSystem;
using TOR_Core.Extensions;
using TOR_Core.Utilities;

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
        private const float BurnTickInterval = 1f;
        private const float DamageVariance = 0.18f;
        private const float ShellVisualScale = 1.15f;
        private const string ShellMeshName = "cannonball_001";
        private const string ImpactParticle = "psys_fireball_explosion_1";
        private const string BurnParticle = "psys_game_burning_agent";
        private const string RicochetSound = "mortar_traveling";

        private static readonly string[] ImpactSounds = { "mortar_explosion_1", "mortar_explosion_2" };
        private static readonly string[] SalvoSounds = { "mortar_shot_1", "mortar_shot_2" };

        private readonly List<ScheduledImpact> _scheduledImpacts = new();
        private readonly List<BurningTarget> _burningTargets = new();

        public void QueueBarrage(Agent caster, Vec3 targetPosition, int guns)
        {
            if (caster == null || Mission.Current == null)
            {
                return;
            }

            var shellCount = EngineerArtilleryBarrageAbility.GetShellCount(guns, caster);
            var radius = EngineerArtilleryBarrageAbility.GetRadius(guns, caster);
            var damage = EngineerArtilleryBarrageAbility.GetImpactDamage(caster);
            var currentTime = Mission.Current.CurrentTime;

            targetPosition.z = Mission.Current.Scene.GetGroundHeightAtPosition(targetPosition);

            for (var i = 0; i < shellCount; i++)
            {
                var impactPosition = GetRandomImpactPosition(targetPosition, radius);
                var impactTime = currentTime + FirstImpactDelay + MBRandom.RandomFloatRanged(0f, MaxRandomImpactDelay);
                var visualStartTime = MBMath.ClampFloat(impactTime - ShellFlightTime, currentTime, impactTime);
                _scheduledImpacts.Add(new ScheduledImpact(
                    impactTime,
                    visualStartTime,
                    caster,
                    GetLaunchSoundPosition(caster.Position),
                    GetShellStartPosition(impactPosition),
                    impactPosition,
                    damage));
            }
        }

        public override void OnMissionTick(float dt)
        {
            if (Mission.Current == null)
            {
                return;
            }

            var currentTime = Mission.Current.CurrentTime;
            UpdateBurningTargets(currentTime);

            if (_scheduledImpacts.Count == 0)
            {
                return;
            }

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
            _burningTargets.Clear();
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

        private static Vec3 GetLaunchSoundPosition(Vec3 casterPosition)
        {
            var angle = MBRandom.RandomFloatRanged(0f, (float)(System.Math.PI * 2.0));
            var distance = MBRandom.RandomFloatRanged(3f, 12f);
            return new Vec3(
                casterPosition.x + (float)System.Math.Cos(angle) * distance,
                casterPosition.y + (float)System.Math.Sin(angle) * distance,
                casterPosition.z);
        }

        private static void UpdateShellVisual(ScheduledImpact impact, float currentTime)
        {
            if (currentTime < impact.VisualStartTime || Mission.Current == null)
            {
                return;
            }

            if (!impact.LaunchSoundPlayed)
            {
                PlayRandomSound(impact.LaunchSoundPosition, SalvoSounds);
                impact.LaunchSoundPlayed = true;
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

            if (!impact.RicochetSoundPlayed && progress >= 0.45f)
            {
                PlaySound(position, RicochetSound);
                impact.RicochetSoundPlayed = true;
            }

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

        private void TriggerImpact(ScheduledImpact impact)
        {
            PlayImpactFeedback(impact.Position);

            var caster = impact.Caster;
            if (caster == null || Mission.Current == null)
            {
                return;
            }

            var hero = caster.GetHero() ?? Hero.MainHero;
            var impactRadius = ImpactRadius + EngineerCareerHelper.GetArtilleryBarrageImpactRadiusBonus(hero);
            var targets = Mission.Current
                .GetNearbyAgents(impact.Position.AsVec2, impactRadius, new MBList<Agent>())
                .Where(agent => IsValidTarget(agent, caster))
                .ToList();

            foreach (var target in targets)
            {
                var distance = target.Position.Distance(impact.Position);
                var falloff = MBMath.ClampFloat(1f - distance / impactRadius, 0.35f, 1f);
                var variance = MBRandom.RandomFloatRanged(1f - DamageVariance, 1f + DamageVariance);
                var damage = MBMath.ClampInt((int)(impact.Damage * falloff * variance), 1, impact.Damage);
                target.ApplyDamage(damage, impact.Position, caster, doBlow: true, hasShockWave: true, originatesFromAbility: true);
                TryApplyBurn(target, caster);
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

        private void TryApplyBurn(Agent target, Agent caster)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            if (!EngineerCareerHelper.ShouldArtilleryBarrageApplyBurn(hero))
            {
                return;
            }

            var duration = EngineerCareerHelper.GetArtilleryBarrageBurnDuration(hero);
            var tickDamage = EngineerCareerHelper.GetArtilleryBarrageBurnDamage(caster);
            var currentTime = Mission.Current.CurrentTime;
            var existing = _burningTargets.FirstOrDefault(x => x.Target == target);
            if (existing != null)
            {
                existing.EndTime = System.Math.Max(existing.EndTime, currentTime + duration);
                existing.TickDamage = System.Math.Max(existing.TickDamage, tickDamage);
                return;
            }

            _burningTargets.Add(new BurningTarget(target, caster, currentTime + duration, currentTime + BurnTickInterval, tickDamage));
            AddBurnFeedback(target.Position);
        }

        private void UpdateBurningTargets(float currentTime)
        {
            if (_burningTargets.Count == 0 || Mission.Current == null)
            {
                return;
            }

            for (var i = _burningTargets.Count - 1; i >= 0; i--)
            {
                var burn = _burningTargets[i];
                if (!IsValidBurnTarget(burn.Target, burn.Caster) || currentTime >= burn.EndTime)
                {
                    _burningTargets.RemoveAt(i);
                    continue;
                }

                if (currentTime < burn.NextTickTime)
                {
                    continue;
                }

                TORMissionHelper.DamageAgents(
                    new[] { burn.Target },
                    burn.TickDamage,
                    burn.TickDamage,
                    burn.Caster,
                    damageType: DamageType.Fire,
                    hasShockWave: false,
                    impactPosition: burn.Target.Position,
                    originSpellTemplate: burn.Caster?.GetCareerAbility()?.Template);

                AddBurnFeedback(burn.Target.Position);
                burn.NextTickTime += BurnTickInterval;
            }
        }

        private static bool IsValidBurnTarget(Agent target, Agent caster)
        {
            return target != null &&
                   caster != null &&
                   target.IsHuman &&
                   target.IsActive() &&
                   target.Health > 0f &&
                   !target.IsFadingOut() &&
                   target.IsEnemyOf(caster);
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

            PlayRandomSound(position, ImpactSounds);
        }

        private static void AddBurnFeedback(Vec3 position)
        {
            var frame = MatrixFrame.Identity;
            frame.origin = position;
            Mission.Current.AddParticleSystemBurstByName(BurnParticle, frame, false);
        }

        private static void PlayRandomSound(Vec3 position, string[] soundNames)
        {
            if (soundNames == null || soundNames.Length == 0)
            {
                return;
            }

            var index = MBRandom.RandomInt(soundNames.Length);
            PlaySound(position, soundNames[index]);
        }

        private sealed class ScheduledImpact
        {
            public ScheduledImpact(float impactTime, float visualStartTime, Agent caster, Vec3 launchSoundPosition, Vec3 startPosition, Vec3 position, int damage)
            {
                ImpactTime = impactTime;
                VisualStartTime = visualStartTime;
                Caster = caster;
                LaunchSoundPosition = launchSoundPosition;
                StartPosition = startPosition;
                Position = position;
                Damage = damage;
            }

            public float ImpactTime { get; }
            public float VisualStartTime { get; }
            public Agent Caster { get; }
            public Vec3 LaunchSoundPosition { get; }
            public Vec3 StartPosition { get; }
            public Vec3 Position { get; }
            public int Damage { get; }
            public GameEntity ProjectileEntity { get; set; }
            public bool LaunchSoundPlayed { get; set; }
            public bool RicochetSoundPlayed { get; set; }
        }

        private sealed class BurningTarget
        {
            public BurningTarget(Agent target, Agent caster, float endTime, float nextTickTime, int tickDamage)
            {
                Target = target;
                Caster = caster;
                EndTime = endTime;
                NextTickTime = nextTickTime;
                TickDamage = tickDamage;
            }

            public Agent Target { get; }
            public Agent Caster { get; }
            public float EndTime { get; set; }
            public float NextTickTime { get; set; }
            public int TickDamage { get; set; }
        }
    }
}
