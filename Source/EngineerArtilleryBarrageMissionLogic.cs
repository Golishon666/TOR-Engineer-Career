using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.AbilitySystem;
using TOR_Core.Extensions;
using TOR_Core.Utilities;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerArtilleryBarrageMissionLogic : MissionLogic
    {
        private const float MaxRandomImpactDelay = 5f;
        private const float ShellFlightTime = 1.4f;
        private const float ShellSpawnHeightMin = 48f;
        private const float ShellSpawnHeightMax = 70f;
        private const float ShellHorizontalDriftMin = 8f;
        private const float ShellHorizontalDriftMax = 18f;
        private const float ImpactRadius = 4.2f;
        private const float DamageVariance = 0.18f;
        private const float GunpowderXpPerDamage = 0.35f;
        private const float EngineeringXpPerDamage = 0.45f;
        private const float ShellVisualScale = 1.15f;
        private const string ShellMeshName = "cannonball_001";
        private const string ImpactParticle = "psys_fireball_explosion_1";
        private const string RicochetSound = "mortar_traveling";

        private static readonly string[] ImpactSounds = { "mortar_explosion_1", "mortar_explosion_2" };
        private static readonly string[] SalvoSounds = { "mortar_shot_1", "mortar_shot_2" };

        private readonly List<ScheduledImpact> _scheduledImpacts = new();
        private readonly Dictionary<int, SoundEvent> _activeSounds = new();
        private readonly List<int> _soundsToRemove = new();

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
                var startPosition = GetShellStartPosition(impactPosition);
                var visualStartTime = currentTime + GetStaggeredLaunchDelay(i, shellCount);
                var impactTime = visualStartTime + ShellFlightTime;
                _scheduledImpacts.Add(new ScheduledImpact(
                    impactTime,
                    visualStartTime,
                    caster,
                    startPosition,
                    startPosition,
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

            ReleaseFinishedSounds();
            var currentTime = Mission.Current.CurrentTime;

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
            ClearRuntimeState();
            base.OnClearScene();
        }

        protected override void OnEndMission()
        {
            ClearRuntimeState();
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

        private static float GetStaggeredLaunchDelay(int shellIndex, int shellCount)
        {
            if (shellCount <= 1)
            {
                return 0f;
            }

            var progress = shellIndex / (float)(shellCount - 1);
            var baseDelay = progress * MaxRandomImpactDelay;
            var jitter = MBRandom.RandomFloatRanged(-0.35f, 0.35f);
            return MBMath.ClampFloat(baseDelay + jitter, 0f, MaxRandomImpactDelay);
        }

        private void UpdateShellVisual(ScheduledImpact impact, float currentTime)
        {
            if (currentTime < impact.VisualStartTime || Mission.Current == null)
            {
                return;
            }

            if (!impact.LaunchSoundPlayed)
            {
                PlayRandomManagedSound(GetAudibleSoundPosition(impact.LaunchSoundPosition), SalvoSounds);
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
                AwardBarrageSkillXp(caster, damage);
                TryChargeOpenFireFromBarrage(caster, damage);
                TryApplyBurn(target, caster);
            }

            EngineerGrenadeExplosionPatch.RegisterExplosionKills(caster, targets.Count(target => !target.IsActive() || target.Health <= 0f));
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
            if (!TORMissionHelper.ApplyStatusEffectToAgent(
                    target,
                    EngineerCareerHelper.ArtilleryBarrageBurnStatusEffectId,
                    caster,
                    duration,
                    append: false,
                    isMutated: false))
            {
                return;
            }

            if (EngineerCareerHelper.ShouldArtilleryBarrageStrengthenBurn(hero))
            {
                TORMissionHelper.ApplyStatusEffectToAgent(
                    target,
                    EngineerCareerHelper.ArtilleryBarrageBurnStatusEffectId,
                    caster,
                    duration,
                    append: false,
                    isMutated: false,
                    stack: true);
            }
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

        private static void TryChargeOpenFireFromBarrage(Agent caster, int damage)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            if (!EngineerCareerHelper.ShouldArtilleryBarrageChargeOpenFire(hero))
            {
                return;
            }

            var careerAbility = caster?.GetComponent<AbilityComponent>()?.CareerAbility
                ?? Agent.Main?.GetComponent<AbilityComponent>()?.CareerAbility;
            if (careerAbility == null || careerAbility.ChargeType != ChargeType.DamageDone || careerAbility.IsActive)
            {
                return;
            }

            careerAbility.AddCharge(EngineerCareerHelper.GetArtilleryBarrageOpenFireCharge(damage));
        }

        private static void AwardBarrageSkillXp(Agent caster, int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            var hero = caster?.GetHero();
            if (!EngineerCareerHelper.IsEngineerHero(hero))
            {
                return;
            }

            var gunpowderSkill = EngineerCareerHelper.GetGunpowderSkill();
            if (gunpowderSkill != null)
            {
                hero.AddSkillXp(gunpowderSkill, damage * GunpowderXpPerDamage);
            }

            hero.AddSkillXp(DefaultSkills.Engineering, damage * EngineeringXpPerDamage);
        }

        private static Vec3 GetAudibleSoundPosition(Vec3 intendedPosition)
        {
            var mission = Mission.Current;
            if (mission == null)
            {
                return intendedPosition;
            }

            var cameraPosition = mission.GetCameraFrame().origin;
            var offset = intendedPosition - cameraPosition;
            if (offset.Length <= 45f)
            {
                return intendedPosition;
            }

            return cameraPosition + offset.NormalizedCopy() * 20f;
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

        private void PlayRandomManagedSound(Vec3 position, string[] soundNames)
        {
            if (soundNames == null || soundNames.Length == 0 || Mission.Current == null)
            {
                return;
            }

            var index = MBRandom.RandomInt(soundNames.Length);
            PlayManagedSound(position, soundNames[index]);
        }

        private void PlayManagedSound(Vec3 position, string soundName)
        {
            var soundIndex = SoundEvent.GetEventIdFromString(soundName);
            if (soundIndex < 0 || Mission.Current == null)
            {
                return;
            }

            var soundEvent = SoundEvent.CreateEvent(soundIndex, Mission.Current.Scene);
            if (soundEvent == null || soundEvent.IsNullSoundEvent())
            {
                soundEvent?.Release();
                Mission.Current.MakeSound(soundIndex, position, false, false, -1, -1);
                return;
            }

            soundEvent.PlayInPosition(position);
            if (!soundEvent.IsValid)
            {
                soundEvent.Release();
                return;
            }

            var id = soundEvent.GetSoundId();
            if (_activeSounds.TryGetValue(id, out var existingSound))
            {
                existingSound?.Release();
            }

            _activeSounds[id] = soundEvent;
        }

        private void ReleaseFinishedSounds()
        {
            if (_activeSounds.Count == 0)
            {
                return;
            }

            _soundsToRemove.Clear();
            foreach (var sound in _activeSounds)
            {
                if (!sound.Value.IsValid || !sound.Value.IsPlaying())
                {
                    _soundsToRemove.Add(sound.Key);
                }
            }

            foreach (var id in _soundsToRemove)
            {
                if (_activeSounds.TryGetValue(id, out var sound))
                {
                    sound?.Release();
                    _activeSounds.Remove(id);
                }
            }
        }

        private void ReleaseAllSounds()
        {
            foreach (var sound in _activeSounds.Values)
            {
                sound?.Stop();
                sound?.Release();
            }

            _activeSounds.Clear();
        }

        private void ClearRuntimeState()
        {
            foreach (var impact in _scheduledImpacts)
            {
                RemoveShellVisual(impact);
            }

            _scheduledImpacts.Clear();
            ReleaseAllSounds();
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

    }
}
