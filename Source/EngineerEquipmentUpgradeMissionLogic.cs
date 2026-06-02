using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TOR_Core.AbilitySystem;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerEquipmentUpgradeMissionLogic : MissionLogic
    {
        public const float PowderKegRadius = 5.5f;
        public const float TargetingBeaconRadius = 8f;
        public const float GrapnelRange = 24f;

        private const float PowderKegDelay = 3f;
        private const float GalvanicDuration = 15f;
        private const float GalvanicRadius = 4f;
        private const float GalvanicTickInterval = 1f;
        private const float TargetingBeaconDuration = 15f;
        private const float FirearmBuffDuration = 20f;
        private const int AethericShotCount = 3;
        private const int RepeaterExtraShotCount = 2;
        private const float SkillXpPerDamage = 0.2f;
        private const string ExplosionParticle = "psys_fireball_explosion_1";

        private static readonly string[] ExplosionSounds = { "mortar_explosion_1", "mortar_explosion_2" };

        private readonly List<DelayedExplosion> _powderKegs = new();
        private readonly List<GalvanicAura> _galvanicAuras = new();
        private readonly List<TargetingBeacon> _targetingBeacons = new();
        private readonly Dictionary<int, TimedCounter> _aethericStabilizers = new();
        private readonly Dictionary<int, TimedCounter> _piercingCalibrations = new();
        private readonly Dictionary<int, TimedCounter> _repeaterCranks = new();
        private int _abilityEnsureAttempts;
        private float _nextAbilityEnsureTime;

        public void ActivateUpgrade(EngineerActiveUpgradeKind activeKind, Agent caster, Vec3 targetPosition)
        {
            switch (activeKind)
            {
                case EngineerActiveUpgradeKind.EmergencyPowderKeg:
                    QueuePowderKeg(caster, targetPosition);
                    break;
                case EngineerActiveUpgradeKind.GalvanicDischarger:
                    ActivateGalvanicDischarger(caster);
                    break;
                case EngineerActiveUpgradeKind.TargetingBeacon:
                    PlaceTargetingBeacon(caster, targetPosition);
                    break;
                case EngineerActiveUpgradeKind.AethericStabilizer:
                    ActivateAethericStabilizer(caster);
                    break;
                case EngineerActiveUpgradeKind.PiercingCalibration:
                    ActivatePiercingCalibration(caster);
                    break;
                case EngineerActiveUpgradeKind.GrapnelLauncher:
                    FireGrapnel(caster, targetPosition);
                    break;
                case EngineerActiveUpgradeKind.PowderReserve:
                    ActivatePowderReserve(caster);
                    break;
                case EngineerActiveUpgradeKind.RepeaterCrank:
                    ActivateRepeaterCrank(caster);
                    break;
            }
        }

        public override void OnMissionTick(float dt)
        {
            if (Mission.Current == null)
            {
                return;
            }

            var currentTime = Mission.Current.CurrentTime;
            EnsureMainAgentUpgradeAbilities(currentTime);
            UpdatePowderKegs(currentTime);
            UpdateGalvanicAuras(currentTime);
            CleanupExpiredCounters(currentTime);
            CleanupExpiredBeacons(currentTime);
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (agent?.IsMainAgent == true)
            {
                EnsureMainAgentUpgradeAbilities(0f, force: true);
            }
        }

        public override void OnAgentControllerSetToPlayer(Agent agent)
        {
            if (agent?.IsMainAgent == true)
            {
                EnsureMainAgentUpgradeAbilities(0f, force: true);
            }
        }

        public override void OnClearScene()
        {
            EngineerGrenadeAmmoPatch.Reset();
            EngineerGrenadeExplosionPatch.Reset();
            ClearRuntimeState();
            ResetAbilityEnsureState();
            base.OnClearScene();
        }

        protected override void OnEndMission()
        {
            EngineerGrenadeAmmoPatch.Reset();
            EngineerGrenadeExplosionPatch.Reset();
            ClearRuntimeState();
            ResetAbilityEnsureState();
        }

        public bool HasAethericStabilizer(Agent agent)
        {
            return HasCounter(_aethericStabilizers, agent);
        }

        public void ConsumeAethericShot(Agent agent)
        {
            ConsumeCounter(_aethericStabilizers, agent);
        }

        public bool TryConsumePiercingCalibration(Agent agent)
        {
            return ConsumeCounter(_piercingCalibrations, agent);
        }

        public bool TryConsumeRepeaterCrank(Agent agent)
        {
            return ConsumeCounter(_repeaterCranks, agent);
        }

        public int GetRepeaterExtraShotCount(Agent agent)
        {
            return RepeaterExtraShotCount;
        }

        public bool IsInsideTargetingBeacon(Agent victim, Agent attacker)
        {
            if (victim == null || attacker == null || _targetingBeacons.Count == 0)
            {
                return false;
            }

            var currentTime = Mission.Current?.CurrentTime ?? 0f;
            return _targetingBeacons.Any(beacon =>
                beacon.EndTime > currentTime &&
                beacon.Caster != null &&
                victim.IsEnemyOf(beacon.Caster) &&
                !attacker.IsEnemyOf(beacon.Caster) &&
                victim.Position.DistanceSquared(beacon.Position) <= TargetingBeaconRadius * TargetingBeaconRadius);
        }

        private void QueuePowderKeg(Agent caster, Vec3 targetPosition)
        {
            if (!IsUsableCaster(caster) || Mission.Current == null)
            {
                return;
            }

            targetPosition = GetGroundPosition(targetPosition);
            _powderKegs.Add(new DelayedExplosion(
                caster,
                targetPosition,
                Mission.Current.CurrentTime + PowderKegDelay,
                GetPowderKegDamage(caster)));

            PlaySound(targetPosition, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_powder_keg_armed}Emergency Powder Keg armed."));
        }

        private void ActivateGalvanicDischarger(Agent caster)
        {
            if (!IsUsableCaster(caster) || Mission.Current == null)
            {
                return;
            }

            RemoveAuraForAgent(caster);
            _galvanicAuras.Add(new GalvanicAura(caster, Mission.Current.CurrentTime + GalvanicDuration));
            PlaySound(caster.Position, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_galvanic_active}Galvanic Discharger active for 15 seconds."));
        }

        private void PlaceTargetingBeacon(Agent caster, Vec3 targetPosition)
        {
            if (!IsUsableCaster(caster) || Mission.Current == null)
            {
                return;
            }

            targetPosition = GetGroundPosition(targetPosition);
            _targetingBeacons.Add(new TargetingBeacon(caster, targetPosition, Mission.Current.CurrentTime + TargetingBeaconDuration));
            PlaySound(targetPosition, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_targeting_beacon}Targeting Beacon placed."));
        }

        private void ActivateAethericStabilizer(Agent caster)
        {
            AddCounter(_aethericStabilizers, caster, AethericShotCount, FirearmBuffDuration);
            PlaySound(caster.Position, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_aetheric_active}Aetheric Stabilizer readied."));
        }

        private void ActivatePiercingCalibration(Agent caster)
        {
            AddCounter(_piercingCalibrations, caster, 1, FirearmBuffDuration);
            PlaySound(caster.Position, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_piercing_active}Piercing Calibration readied."));
        }

        private void ActivateRepeaterCrank(Agent caster)
        {
            AddCounter(_repeaterCranks, caster, 1, FirearmBuffDuration);
            PlaySound(caster.Position, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_repeater_active}Repeater Crank readied."));
        }

        private void FireGrapnel(Agent caster, Vec3 targetPosition)
        {
            if (!IsUsableCaster(caster) || Mission.Current == null)
            {
                return;
            }

            targetPosition = ClampToRange(caster.Position, GetGroundPosition(targetPosition), GrapnelRange);
            targetPosition.z += 0.05f;
            caster.TeleportToPosition(targetPosition);
            PlaySound(targetPosition, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_grapnel_fired}Grapnel Launcher fired."));
        }

        private void ActivatePowderReserve(Agent caster)
        {
            if (!IsUsableCaster(caster))
            {
                return;
            }

            var restored = 0;
            for (var i = 0; i < 5; i++)
            {
                var slot = (EquipmentIndex)i;
                var weapon = caster.Equipment[slot];
                if (weapon.IsEmpty || !weapon.IsAnyAmmo() || weapon.Item == null)
                {
                    continue;
                }

                var maxAmount = weapon.Item.PrimaryWeapon?.MaxDataValue ?? weapon.Amount;
                if (maxAmount <= weapon.Amount)
                {
                    continue;
                }

                var restoreAmount = weapon.Item.IsSpecialAmmunitionItem() || EngineerCareerHelper.IsEngineerGrenadeItem(weapon.Item) ? 1 : 8;
                var targetAmount = (short)MBMath.ClampInt(weapon.Amount + restoreAmount, weapon.Amount, maxAmount);
                if (targetAmount <= weapon.Amount)
                {
                    continue;
                }

                restored += targetAmount - weapon.Amount;
                caster.SetWeaponAmountInSlot(slot, targetAmount, true);
            }

            PlaySound(caster.Position, "mortar_traveling");
            MBInformationManager.AddQuickInformation(new TextObject("{=tor_engineer_powder_reserve}Powder Reserve restored {COUNT} ammo.")
                .SetTextVariable("COUNT", restored));
        }

        private void UpdatePowderKegs(float currentTime)
        {
            for (var i = _powderKegs.Count - 1; i >= 0; i--)
            {
                var keg = _powderKegs[i];
                if (keg.TriggerTime > currentTime)
                {
                    continue;
                }

                Detonate(keg);
                _powderKegs.RemoveAt(i);
            }
        }

        private void UpdateGalvanicAuras(float currentTime)
        {
            for (var i = _galvanicAuras.Count - 1; i >= 0; i--)
            {
                var aura = _galvanicAuras[i];
                if (!IsUsableCaster(aura.Caster) || aura.EndTime <= currentTime)
                {
                    _galvanicAuras.RemoveAt(i);
                    continue;
                }

                if (aura.NextTickTime > currentTime)
                {
                    continue;
                }

                aura.NextTickTime = currentTime + GalvanicTickInterval;
                ShockNearbyEnemies(aura.Caster);
            }
        }

        private void CleanupExpiredCounters(float currentTime)
        {
            CleanupExpiredCounters(_aethericStabilizers, currentTime);
            CleanupExpiredCounters(_piercingCalibrations, currentTime);
            CleanupExpiredCounters(_repeaterCranks, currentTime);
        }

        private void CleanupExpiredBeacons(float currentTime)
        {
            for (var i = _targetingBeacons.Count - 1; i >= 0; i--)
            {
                if (_targetingBeacons[i].EndTime <= currentTime || !IsUsableCaster(_targetingBeacons[i].Caster))
                {
                    _targetingBeacons.RemoveAt(i);
                }
            }
        }

        private void EnsureMainAgentUpgradeAbilities(float currentTime, bool force = false)
        {
            if (!force && (_abilityEnsureAttempts >= 12 || currentTime < _nextAbilityEnsureTime))
            {
                return;
            }

            var mainAgent = Agent.Main;
            var component = mainAgent?.GetComponent<AbilityComponent>();
            if (mainAgent == null || component == null)
            {
                _abilityEnsureAttempts++;
                _nextAbilityEnsureTime = currentTime + 1f;
                SubModule.Log($"Mission ensure abilities: mainAgent={(mainAgent != null)}, component={(component != null)}, attempt={_abilityEnsureAttempts}.");
                return;
            }

            _abilityEnsureAttempts++;
            _nextAbilityEnsureTime = currentTime + 1f;
            var addedAny = EngineerEquipmentUpgradeAbilityComponentPatch.EnsureAbilities(
                component,
                mainAgent,
                $"MissionTick#{_abilityEnsureAttempts}");

            if (addedAny)
            {
                _abilityEnsureAttempts = 12;
            }
        }

        private void Detonate(DelayedExplosion keg)
        {
            PlayExplosionFeedback(keg.Position);
            var targets = Mission.Current.GetNearbyAgents(keg.Position.AsVec2, PowderKegRadius, new MBList<Agent>())
                         .Where(agent => IsValidTarget(agent, keg.Caster))
                         .ToList();

            foreach (var target in targets)
            {
                var distance = target.Position.Distance(keg.Position);
                var falloff = MBMath.ClampFloat(1f - distance / PowderKegRadius, 0.35f, 1f);
                var damage = MBMath.ClampInt((int)(keg.Damage * falloff), 1, keg.Damage);
                target.ApplyDamage(damage, keg.Position, keg.Caster, doBlow: true, hasShockWave: true, originatesFromAbility: true);
                AwardSkillXp(keg.Caster, damage);
            }

            EngineerGrenadeExplosionPatch.RegisterExplosionKills(keg.Caster, targets.Count(target => !target.IsActive() || target.Health <= 0f));
        }

        private void ShockNearbyEnemies(Agent caster)
        {
            var targets = Mission.Current.GetNearbyAgents(caster.Position.AsVec2, GalvanicRadius, new MBList<Agent>())
                         .Where(agent => IsValidTarget(agent, caster))
                         .ToList();

            foreach (var target in targets)
            {
                var damage = GetGalvanicDamage(caster);
                target.ApplyDamage(damage, caster.Position, caster, doBlow: true, hasShockWave: true, originatesFromAbility: true);
                AwardSkillXp(caster, damage);
            }
        }

        private static bool IsUsableCaster(Agent caster)
        {
            return caster != null && caster.IsActive() && caster.Health > 0f;
        }

        private static bool IsValidTarget(Agent target, Agent caster)
        {
            return target != null &&
                   target.IsHuman &&
                   target.IsActive() &&
                   target.Health > 0f &&
                   !target.IsFadingOut() &&
                   target.IsEnemyOf(caster);
        }

        private static int GetPowderKegDamage(Agent caster)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            var engineering = hero?.GetSkillValue(DefaultSkills.Engineering) ?? 0;
            var gunpowderSkill = EngineerCareerHelper.GetGunpowderSkill();
            var gunpowder = hero == null || gunpowderSkill == null ? 0 : hero.GetSkillValue(gunpowderSkill);
            return MBMath.ClampInt((int)(75f + engineering * 0.20f + gunpowder * 0.25f), 75, 170);
        }

        private static int GetGalvanicDamage(Agent caster)
        {
            var hero = caster?.GetHero() ?? Hero.MainHero;
            var engineering = hero?.GetSkillValue(DefaultSkills.Engineering) ?? 0;
            return MBMath.ClampInt((int)(8f + engineering * 0.03f), 8, 24);
        }

        private static void AwardSkillXp(Agent caster, int damage)
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

            hero.AddSkillXp(DefaultSkills.Engineering, damage * SkillXpPerDamage);
            var gunpowderSkill = EngineerCareerHelper.GetGunpowderSkill();
            if (gunpowderSkill != null)
            {
                hero.AddSkillXp(gunpowderSkill, damage * SkillXpPerDamage);
            }
        }

        private static Vec3 GetGroundPosition(Vec3 position)
        {
            var scene = Mission.Current?.Scene;
            if (scene != null)
            {
                position.z = scene.GetGroundHeightAtPosition(position);
            }

            return position;
        }

        private static Vec3 ClampToRange(Vec3 origin, Vec3 target, float range)
        {
            var offset = target - origin;
            offset.z = 0f;
            if (offset.Length <= range)
            {
                return target;
            }

            return origin + offset.NormalizedCopy() * range;
        }

        private static void AddCounter(Dictionary<int, TimedCounter> counters, Agent agent, int count, float duration)
        {
            if (!IsUsableCaster(agent) || Mission.Current == null)
            {
                return;
            }

            counters[agent.Index] = new TimedCounter(count, Mission.Current.CurrentTime + duration);
        }

        private static bool HasCounter(Dictionary<int, TimedCounter> counters, Agent agent)
        {
            if (agent == null || Mission.Current == null || !counters.TryGetValue(agent.Index, out var counter))
            {
                return false;
            }

            return counter.Count > 0 && counter.EndTime > Mission.Current.CurrentTime;
        }

        private static bool ConsumeCounter(Dictionary<int, TimedCounter> counters, Agent agent)
        {
            if (!HasCounter(counters, agent))
            {
                return false;
            }

            var counter = counters[agent.Index];
            counter.Count--;
            if (counter.Count <= 0)
            {
                counters.Remove(agent.Index);
            }

            return true;
        }

        private static void CleanupExpiredCounters(Dictionary<int, TimedCounter> counters, float currentTime)
        {
            var expiredKeys = counters
                .Where(entry => entry.Value.Count <= 0 || entry.Value.EndTime <= currentTime)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                counters.Remove(key);
            }
        }

        private void RemoveAuraForAgent(Agent agent)
        {
            for (var i = _galvanicAuras.Count - 1; i >= 0; i--)
            {
                if (_galvanicAuras[i].Caster == agent)
                {
                    _galvanicAuras.RemoveAt(i);
                }
            }
        }

        private void ClearRuntimeState()
        {
            _powderKegs.Clear();
            _galvanicAuras.Clear();
            _targetingBeacons.Clear();
            _aethericStabilizers.Clear();
            _piercingCalibrations.Clear();
            _repeaterCranks.Clear();
        }

        private void ResetAbilityEnsureState()
        {
            _abilityEnsureAttempts = 0;
            _nextAbilityEnsureTime = 0f;
        }

        private static void PlayExplosionFeedback(Vec3 position)
        {
            var frame = MatrixFrame.Identity;
            frame.origin = position;
            Mission.Current.AddParticleSystemBurstByName(ExplosionParticle, frame, false);

            var index = MBRandom.RandomInt(ExplosionSounds.Length);
            PlaySound(position, ExplosionSounds[index]);
        }

        private static void PlaySound(Vec3 position, string soundName)
        {
            var soundIndex = SoundEvent.GetEventIdFromString(soundName);
            if (soundIndex >= 0 && Mission.Current != null)
            {
                Mission.Current.MakeSound(soundIndex, position, false, false, -1, -1);
            }
        }

        private sealed class DelayedExplosion
        {
            public DelayedExplosion(Agent caster, Vec3 position, float triggerTime, int damage)
            {
                Caster = caster;
                Position = position;
                TriggerTime = triggerTime;
                Damage = damage;
            }

            public Agent Caster { get; }
            public Vec3 Position { get; }
            public float TriggerTime { get; }
            public int Damage { get; }
        }

        private sealed class GalvanicAura
        {
            public GalvanicAura(Agent caster, float endTime)
            {
                Caster = caster;
                EndTime = endTime;
                NextTickTime = 0f;
            }

            public Agent Caster { get; }
            public float EndTime { get; }
            public float NextTickTime { get; set; }
        }

        private sealed class TargetingBeacon
        {
            public TargetingBeacon(Agent caster, Vec3 position, float endTime)
            {
                Caster = caster;
                Position = position;
                EndTime = endTime;
            }

            public Agent Caster { get; }
            public Vec3 Position { get; }
            public float EndTime { get; }
        }

        private sealed class TimedCounter
        {
            public TimedCounter(int count, float endTime)
            {
                Count = count;
                EndTime = endTime;
            }

            public int Count { get; set; }
            public float EndTime { get; }
        }
    }
}
