using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TOR_Core.AbilitySystem;
using TOR_Core.BattleMechanics;
using TOR_Core.BattleMechanics.DamageSystem;
using TOR_Core.BattleMechanics.StatusEffect;
using TOR_Core.BattleMechanics.TriggeredEffect;
using TOR_Core.CampaignMechanics.Choices;
using TOR_Core.CharacterDevelopment;
using TOR_Core.CharacterDevelopment.CareerSystem;
using TOR_Core.Extensions;
using TOR_Core.Extensions.ExtendedInfoSystem;

namespace TOR_EngineerCareer
{
    internal sealed class EngineerCareerChoices : TORCareerChoicesBase
    {
        private CareerChoiceObject _engineerRoot;

        private readonly Dictionary<string, CareerChoiceObject> _choices = new();

        public EngineerCareerChoices(CareerObject careerId) : base(careerId)
        {
        }

        protected override void RegisterAll()
        {
            _engineerRoot = Register("EngineerRoot");

            RegisterBranch("PowderDrill");
            RegisterBranch("FieldTesting");
            RegisterBranch("ExplosiveRounds");
            RegisterBranch("SuppressionFire");
            RegisterBranch("Grenadier");
            RegisterBranch("PiercingDoctrine");
            RegisterBranch("RicochetTactics");
            RegisterBranch("IncendiaryFuses");
            RegisterBranch("GrandBattery");
        }

        protected override void InitializeKeyStones()
        {
            if (CareerID == null || _engineerRoot == null)
            {
                SubModule.Log("Engineer career choices skipped keystone init: career or root choice is missing.");
                return;
            }

            _engineerRoot.Initialize(CareerID,
                EngineerCareerHelper.BuildOpenFireDescriptionText(),
                null,
                true,
                ChoiceType.Keystone,
                CombineMutations(
                    RenameOpenFire(),
                    MutateLetThemHaveIt("let_them_have_it_range_dmg", "let_them_have_it_melee_rls")));

            Keystone("PowderDrill",
                "Open Fire! lasts longer; Gunpowder tightens the drill.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Duration), EngineerCareerHelper.PowderDrillDurationBonus),
                    MutateStatusAdd("let_them_have_it_melee_rls", 0.10f),
                    MutateStatusSkillScale("let_them_have_it_melee_rls", () => EngineerCareerHelper.GetGunpowderSkill(), EngineerCareerHelper.GunPowderEffectScale),
                    MutateStatusSkillScale("let_them_have_it_range_dmg", () => EngineerCareerHelper.GetGunpowderSkill(), EngineerCareerHelper.GunPowderEffectScale)));

            Keystone("FieldTesting",
                "Open Fire! hardens the line; Athletics improves the brace.",
                CombineMutations(
                    MutateLetThemHaveIt("let_them_have_it_range_res"),
                    MutateAbilitySkillScale(nameof(AbilityTemplate.Duration), () => EngineerCareerHelper.GetSkill("Athletics"), EngineerCareerHelper.AthleticsDurationScale),
                    MutateStatusSkillScale("let_them_have_it_range_res", () => EngineerCareerHelper.GetSkill("Athletics"), 0.0005f)));

            Keystone("ExplosiveRounds",
                "Bullets burst on impact; Open Fire! throws blasts wider.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Radius), 0.5f),
                    MutateLetThemHaveIt("let_them_have_it_range_dmg")));

            Keystone("SuppressionFire",
                "Open Fire! covers more ground and steadies the line.",
                CombineMutations(
                    MutateTriggeredEffectFloat("apply_let_them_have_it", nameof(TriggeredEffectTemplate.Radius), 2f),
                    MutateLetThemHaveIt("let_them_have_it_range_res")));

            Keystone("Grenadier",
                "Every 7 explosive kills refunds a charge.",
                CombineMutations(
                    MutateStatusSkillScale("let_them_have_it_range_dmg", () => EngineerCareerHelper.GetSkill("Throwing"), EngineerCareerHelper.ThrowingEffectScale),
                    MutateStatusSkillScale("let_them_have_it_melee_rls", () => EngineerCareerHelper.GetSkill("Throwing"), EngineerCareerHelper.ThrowingEffectScale)));

            Keystone("PiercingDoctrine",
                "Bullets punch through and keep looking for trouble.",
                MutateStatusAdd("let_them_have_it_range_dmg", 0.15f));

            Keystone("RicochetTactics",
                "Open Fire! turns rebounds into 30% detonation chances.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Duration), EngineerCareerHelper.RicochetTacticsDurationBonus),
                    MutateTriggeredEffectFloat("apply_let_them_have_it", nameof(TriggeredEffectTemplate.Radius), 2f)));

            Keystone("IncendiaryFuses",
                "Barrage learns to burn, linger and feed Open Fire!",
                NoMutations());

            Keystone("GrandBattery",
                "Barrage fields +1 max gun and +1 shell per gun.",
                NoMutations());
        }

        protected override void InitializePassives()
        {
            Passive("PowderDrill", 1, "+6 ammo per pouch. Every cartridge has its place.", Ammo());
            Passive("PowderDrill", 2, "+10% gunpowder damage. Better powder, louder answers.", GunpowderDamage(10));
            Passive("PowderDrill", 3, "+30% firearm accuracy. Rifling, gauges and steady hands.", new CareerChoiceObject.PassiveEffect(-30, PassiveEffectType.AccuracyPenalty, true));
            Passive("PowderDrill", 4, "Ranged troops gain +25 Gunpowder from your field drills.", new CareerChoiceObject.PassiveEffect(25, new List<string> { nameof(TORSkills.GunPowder) }, IsRangedTroop));

            Passive("FieldTesting", 1, "+6 ammo per pouch. Test loads survive the march.", Ammo());
            Passive("FieldTesting", 2, "+15% ranged resist. Duck, brace, return fire.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Resistance, new DamageProportionTuple(DamageType.Physical, 15), AttackTypeMask.Ranged));
            Passive("FieldTesting", 3, "+15% firearm speed. Hotter charges fly faster.", SpecialPassive());
            Passive("FieldTesting", 4, "+1 map speed. Surveyor routes keep the column moving.", new CareerChoiceObject.PassiveEffect(1f, PassiveEffectType.PartyMovementSpeed));

            Passive("ExplosiveRounds", 1, "+6 ammo per pouch. Extra rounds for volatile work.", Ammo());
            Passive("ExplosiveRounds", 2, "No ranged stagger. Shrapnel will not break your aim.", SpecialPassive());
            Passive("ExplosiveRounds", 3, "Ranged troops deal +10% damage under your firing tables.", TroopRangedDamage(10));
            Passive("ExplosiveRounds", 4, "+10% ranged pierce. Hardened shot finds the gap.", new CareerChoiceObject.PassiveEffect(-10, PassiveEffectType.ArmorPenetration, AttackTypeMask.Ranged));

            Passive("SuppressionFire", 1, "+6 ammo per pouch. Suppression needs deep pockets.", Ammo());
            Passive("SuppressionFire", 2, "Ranged troops gain +10% resist while holding the line.", TroopRangedResistance(10));
            Passive("SuppressionFire", 3, "+10% gunpowder damage. Keep their heads down.", GunpowderDamage(10));
            Passive("SuppressionFire", 4, "+10 party size. More barrels for the battery.", new CareerChoiceObject.PassiveEffect(10, PassiveEffectType.PartySize));

            Passive("Grenadier", 1, "+3 explosive charges per pouch. Pack them tight.", new CareerChoiceObject.PassiveEffect(3, PassiveEffectType.Special, false));
            Passive("Grenadier", 2, "+30% explosion radius. The blast grows greedy.", SpecialPassive());
            Passive("Grenadier", 3, "+30% Fire damage. The fuse bites deeper.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Damage, new DamageProportionTuple(DamageType.Fire, 30), AttackTypeMask.Ranged));
            Passive("Grenadier", 4, "+30% explosive speed. Arcs flatten, fuses hurry.", SpecialPassive());

            Passive("PiercingDoctrine", 1, "+6 ammo per pouch. Piercing work takes practice.", Ammo());
            Passive("PiercingDoctrine", 2, "+25% ranged pierce. Measure armor, then ignore it.", new CareerChoiceObject.PassiveEffect(-25, PassiveEffectType.ArmorPenetration, AttackTypeMask.Ranged));
            Passive("PiercingDoctrine", 3, "+20% gunpowder damage. Pressure wins arguments.", GunpowderDamage(20));
            Passive("PiercingDoctrine", 4, "Ranged troops deal +15% damage with drilled volleys.", TroopRangedDamage(15));

            Passive("RicochetTactics", 1, "+6 ammo per pouch. Spare shot for improbable angles.", Ammo());
            Passive("RicochetTactics", 2, "+15% ranged resist. Cover, angles and stubborn helmets.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Resistance, new DamageProportionTuple(DamageType.Physical, 15), AttackTypeMask.Ranged));
            Passive("RicochetTactics", 3, "+30% firearm accuracy. Calculated rebounds start true.", new CareerChoiceObject.PassiveEffect(-30, PassiveEffectType.AccuracyPenalty, true));
            Passive("RicochetTactics", 4, "Ricochets always happen; 30% detonate; buckshot +3.", SpecialPassive());

            Passive("IncendiaryFuses", 1, "Barrage: +10% damage, ignites, charges Open Fire!", SpecialPassive());
            Passive("IncendiaryFuses", 2, "Barrage burn lasts +2s and can stack twice.", SpecialPassive());
            Passive("IncendiaryFuses", 3, "Barrage cooldown -5s. The crews reload hot.", SpecialPassive());
            Passive("IncendiaryFuses", 4, "+30% Fire damage. Everything burns brighter.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Damage, new DamageProportionTuple(DamageType.Fire, 30), AttackTypeMask.Ranged));

            Passive("GrandBattery", 1, "Barrage fires +2 shells. The battery speaks longer.", SpecialPassive());
            Passive("GrandBattery", 2, "Barrage deals +10% damage. Heavier powder, harder fall.", SpecialPassive());
            Passive("GrandBattery", 3, "Barrage impacts cover a wider killing ground.", SpecialPassive());
            Passive("GrandBattery", 4, "Barrage fires +2 shells and reloads 5s sooner.", SpecialPassive());
        }

        private static CareerChoiceObject Register(string id)
        {
            var objectManager = Game.Current?.ObjectManager ?? Campaign.Current?.ObjectManager;
            return objectManager.RegisterPresumedObject(new CareerChoiceObject(id));
        }

        private void RegisterBranch(string groupId)
        {
            _choices[groupId + "Keystone"] = Register(groupId + "Keystone");
            for (var i = 1; i <= 4; i++)
            {
                _choices[groupId + "Passive" + i] = Register(groupId + "Passive" + i);
            }
        }

        private void Keystone(string groupId, string description, List<CareerChoiceObject.MutationObject> mutations)
        {
            if (!_choices.TryGetValue(groupId + "Keystone", out var choice) || choice == null)
            {
                SubModule.Log($"Engineer keystone '{groupId}' was not registered.");
                return;
            }

            choice.Initialize(CareerID, description, groupId, false, ChoiceType.Keystone, mutations);
        }

        private void Passive(string groupId, int index, string description, CareerChoiceObject.PassiveEffect passive)
        {
            if (!_choices.TryGetValue(groupId + "Passive" + index, out var choice) || choice == null)
            {
                SubModule.Log($"Engineer passive '{groupId}{index}' was not registered.");
                return;
            }

            choice.Initialize(CareerID, description, groupId, false, ChoiceType.Passive, null, passive);
        }

        private static CareerChoiceObject.PassiveEffect Ammo()
        {
            return new CareerChoiceObject.PassiveEffect(6, PassiveEffectType.Ammo);
        }

        private static CareerChoiceObject.PassiveEffect SpecialPassive()
        {
            return new CareerChoiceObject.PassiveEffect(0, PassiveEffectType.Special);
        }

        private static CareerChoiceObject.PassiveEffect GunpowderDamage(int percent)
        {
            return new CareerChoiceObject.PassiveEffect(PassiveEffectType.Damage,
                new DamageProportionTuple(DamageType.Physical, percent),
                AttackTypeMask.Ranged,
                (attacker, victim, mask) => mask == AttackTypeMask.Ranged && attacker != null && attacker.IsMainAgent && IsUsingGunpowder(attacker));
        }

        private static CareerChoiceObject.PassiveEffect TroopRangedDamage(int percent)
        {
            return new CareerChoiceObject.PassiveEffect(PassiveEffectType.TroopDamage,
                new DamageProportionTuple(DamageType.Physical, percent),
                AttackTypeMask.Ranged,
                (attacker, victim, mask) => mask == AttackTypeMask.Ranged && attacker != null && attacker.BelongsToMainParty() && !attacker.IsMainAgent && attacker.Character?.IsRanged == true);
        }

        private static CareerChoiceObject.PassiveEffect TroopRangedResistance(int percent)
        {
            return new CareerChoiceObject.PassiveEffect(PassiveEffectType.TroopResistance,
                new DamageProportionTuple(DamageType.Physical, percent),
                AttackTypeMask.Ranged,
                (attacker, victim, mask) => mask == AttackTypeMask.Ranged && victim != null && victim.BelongsToMainParty() && !victim.IsMainAgent && victim.Character?.IsRanged == true);
        }

        private static bool IsUsingGunpowder(Agent agent)
        {
            return agent?.WieldedWeapon.CurrentUsageItem != null &&
                   agent.WieldedWeapon.CurrentUsageItem.IsGunPowderWeapon();
        }

        private static bool IsRangedTroop(CharacterObject character)
        {
            return character != null && !character.IsHero && character.IsRanged;
        }

        private static List<CareerChoiceObject.MutationObject> RenameOpenFire()
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(AbilityTemplate),
                    MutationTargetOriginalId = "LetThemHaveIt",
                    PropertyName = nameof(AbilityTemplate.Name),
                    MutationType = OperationType.Replace,
                    PropertyValue = (choice, originalValue, agent) => "Open Fire!"
                },
                new()
                {
                    MutationTargetType = typeof(AbilityTemplate),
                    MutationTargetOriginalId = "LetThemHaveIt",
                    PropertyName = nameof(AbilityTemplate.SpriteName),
                    MutationType = OperationType.Replace,
                    PropertyValue = (choice, originalValue, agent) => EngineerCareerHelper.OpenFireIconSprite
                },
                new()
                {
                    MutationTargetType = typeof(AbilityTemplate),
                    MutationTargetOriginalId = "LetThemHaveIt",
                    PropertyName = nameof(AbilityTemplate.CoolDown),
                    MutationType = OperationType.Replace,
                    PropertyValue = (choice, originalValue, agent) => 0
                },
                new()
                {
                    MutationTargetType = typeof(AbilityTemplate),
                    MutationTargetOriginalId = "LetThemHaveIt",
                    PropertyName = nameof(AbilityTemplate.TooltipDescription),
                    MutationType = OperationType.Replace,
                    PropertyValue = (choice, originalValue, agent) =>
                        EngineerCareerHelper.BuildOpenFireDescriptionText()
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> MutateLetThemHaveIt(params string[] statusEffects)
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(TriggeredEffectTemplate),
                    MutationTargetOriginalId = "apply_let_them_have_it",
                    PropertyName = nameof(TriggeredEffectTemplate.ImbuedStatusEffects),
                    MutationType = OperationType.Replace,
                    PropertyValue = (choice, originalValue, agent) =>
                        ((List<string>)originalValue).Concat(statusEffects).Distinct().ToList()
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> MutateStatusAdd(string statusId, float value)
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(StatusEffectTemplate),
                    MutationTargetOriginalId = statusId,
                    PropertyName = nameof(StatusEffectTemplate.BaseEffectValue),
                    MutationType = OperationType.Add,
                    PropertyValue = (choice, originalValue, agent) => value
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> MutateStatusSkillScale(string statusId, SkillObject skill, float scale)
        {
            return MutateStatusSkillScale(statusId, () => skill, scale);
        }

        private static List<CareerChoiceObject.MutationObject> MutateStatusSkillScale(string statusId, System.Func<SkillObject> skillResolver, float scale)
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(StatusEffectTemplate),
                    MutationTargetOriginalId = statusId,
                    PropertyName = nameof(StatusEffectTemplate.BaseEffectValue),
                    MutationType = OperationType.Add,
                    PropertyValue = (choice, originalValue, agent) =>
                    {
                        var skill = skillResolver();
                        return skill == null
                            ? 0f
                            : CareerHelper.AddSkillEffectToValue(choice, agent, new List<SkillObject> { skill }, scale);
                    }
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> MutateAbilityFloat(string propertyName, float value)
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(AbilityTemplate),
                    MutationTargetOriginalId = "LetThemHaveIt",
                    PropertyName = propertyName,
                    MutationType = OperationType.Add,
                    PropertyValue = (choice, originalValue, agent) => value
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> MutateAbilitySkillScale(string propertyName, System.Func<SkillObject> skillResolver, float scale)
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(AbilityTemplate),
                    MutationTargetOriginalId = "LetThemHaveIt",
                    PropertyName = propertyName,
                    MutationType = OperationType.Add,
                    PropertyValue = (choice, originalValue, agent) =>
                    {
                        var skill = skillResolver();
                        return skill == null
                            ? 0f
                            : CareerHelper.AddSkillEffectToValue(choice, agent, new List<SkillObject> { skill }, scale);
                    }
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> MutateTriggeredEffectFloat(string effectId, string propertyName, float value)
        {
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(TriggeredEffectTemplate),
                    MutationTargetOriginalId = effectId,
                    PropertyName = propertyName,
                    MutationType = OperationType.Add,
                    PropertyValue = (choice, originalValue, agent) => value
                }
            };
        }

        private static List<CareerChoiceObject.MutationObject> CombineMutations(params List<CareerChoiceObject.MutationObject>[] mutations)
        {
            return mutations.SelectMany(x => x).ToList();
        }

        private static List<CareerChoiceObject.MutationObject> NoMutations()
        {
            return new List<CareerChoiceObject.MutationObject>();
        }
    }
}
