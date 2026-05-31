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
                "Open Fire!: +5s; scales Gunpowder.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Duration), EngineerCareerHelper.PowderDrillDurationBonus),
                    MutateStatusAdd("let_them_have_it_melee_rls", 0.10f),
                    MutateStatusSkillScale("let_them_have_it_melee_rls", () => EngineerCareerHelper.GetGunpowderSkill(), EngineerCareerHelper.GunPowderEffectScale),
                    MutateStatusSkillScale("let_them_have_it_range_dmg", () => EngineerCareerHelper.GetGunpowderSkill(), EngineerCareerHelper.GunPowderEffectScale)));

            Keystone("FieldTesting",
                "Open Fire!: resist; scales Athletics.",
                CombineMutations(
                    MutateLetThemHaveIt("let_them_have_it_range_res"),
                    MutateAbilitySkillScale(nameof(AbilityTemplate.Duration), () => EngineerCareerHelper.GetSkill("Athletics"), EngineerCareerHelper.AthleticsDurationScale),
                    MutateStatusSkillScale("let_them_have_it_range_res", () => EngineerCareerHelper.GetSkill("Athletics"), 0.0005f)));

            Keystone("ExplosiveRounds",
                "Bullets explode; Open Fire! +radius.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Radius), 0.5f),
                    MutateLetThemHaveIt("let_them_have_it_range_dmg")));

            Keystone("SuppressionFire",
                "Open Fire!: +area, +resist.",
                CombineMutations(
                    MutateTriggeredEffectFloat("apply_let_them_have_it", nameof(TriggeredEffectTemplate.Radius), 2f),
                    MutateLetThemHaveIt("let_them_have_it_range_res")));

            Keystone("Grenadier",
                "15-kill grenades refund.",
                CombineMutations(
                    MutateStatusSkillScale("let_them_have_it_range_dmg", () => EngineerCareerHelper.GetSkill("Throwing"), EngineerCareerHelper.ThrowingEffectScale),
                    MutateStatusSkillScale("let_them_have_it_melee_rls", () => EngineerCareerHelper.GetSkill("Throwing"), EngineerCareerHelper.ThrowingEffectScale)));

            Keystone("PiercingDoctrine",
                "Bullets overpenetrate.",
                MutateStatusAdd("let_them_have_it_range_dmg", 0.15f));

            Keystone("RicochetTactics",
                "Open Fire!: ricochet; 30% detonate.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Duration), EngineerCareerHelper.RicochetTacticsDurationBonus),
                    MutateTriggeredEffectFloat("apply_let_them_have_it", nameof(TriggeredEffectTemplate.Radius), 2f)));

            Keystone("IncendiaryFuses",
                "Barrage: incendiary upgrades.",
                NoMutations());

            Keystone("GrandBattery",
                "Barrage: +1 max gun, +1 shell per gun.",
                NoMutations());
        }

        protected override void InitializePassives()
        {
            Passive("PowderDrill", 1, "+6 ammo per pouch.", Ammo());
            Passive("PowderDrill", 2, "+10% gunpowder damage.", GunpowderDamage(10));
            Passive("PowderDrill", 3, "+15% firearm accuracy.", new CareerChoiceObject.PassiveEffect(-15, PassiveEffectType.AccuracyPenalty, true));
            Passive("PowderDrill", 4, "Ranged troops: +25 Gunpowder.", new CareerChoiceObject.PassiveEffect(25, new List<string> { nameof(TORSkills.GunPowder) }, IsRangedTroop));

            Passive("FieldTesting", 1, "+6 ammo per pouch.", Ammo());
            Passive("FieldTesting", 2, "+15% ranged resist.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Resistance, new DamageProportionTuple(DamageType.Physical, 15), AttackTypeMask.Ranged));
            Passive("FieldTesting", 3, "+15% firearm speed.", SpecialPassive());
            Passive("FieldTesting", 4, "+1 map speed.", new CareerChoiceObject.PassiveEffect(1f, PassiveEffectType.PartyMovementSpeed));

            Passive("ExplosiveRounds", 1, "+6 ammo per pouch.", Ammo());
            Passive("ExplosiveRounds", 2, "No ranged stagger.", SpecialPassive());
            Passive("ExplosiveRounds", 3, "Ranged troops: +10% damage.", TroopRangedDamage(10));
            Passive("ExplosiveRounds", 4, "+10% ranged pierce.", new CareerChoiceObject.PassiveEffect(-10, PassiveEffectType.ArmorPenetration, AttackTypeMask.Ranged));

            Passive("SuppressionFire", 1, "+6 ammo per pouch.", Ammo());
            Passive("SuppressionFire", 2, "Ranged troops: +10% resist.", TroopRangedResistance(10));
            Passive("SuppressionFire", 3, "+10% gunpowder damage.", GunpowderDamage(10));
            Passive("SuppressionFire", 4, "+10 party size.", new CareerChoiceObject.PassiveEffect(10, PassiveEffectType.PartySize));

            Passive("Grenadier", 1, "+3 grenades per pouch.", new CareerChoiceObject.PassiveEffect(3, PassiveEffectType.Special, false));
            Passive("Grenadier", 2, "+30% grenade radius.", SpecialPassive());
            Passive("Grenadier", 3, "+30% Fire damage.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Damage, new DamageProportionTuple(DamageType.Fire, 30), AttackTypeMask.Ranged));
            Passive("Grenadier", 4, "+30% grenade speed.", SpecialPassive());

            Passive("PiercingDoctrine", 1, "+6 ammo per pouch.", Ammo());
            Passive("PiercingDoctrine", 2, "+25% ranged pierce.", new CareerChoiceObject.PassiveEffect(-25, PassiveEffectType.ArmorPenetration, AttackTypeMask.Ranged));
            Passive("PiercingDoctrine", 3, "+20% gunpowder damage.", GunpowderDamage(20));
            Passive("PiercingDoctrine", 4, "Ranged troops: +15% damage.", TroopRangedDamage(15));

            Passive("RicochetTactics", 1, "+6 ammo per pouch.", Ammo());
            Passive("RicochetTactics", 2, "+15% ranged resist.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Resistance, new DamageProportionTuple(DamageType.Physical, 15), AttackTypeMask.Ranged));
            Passive("RicochetTactics", 3, "+15% firearm accuracy.", new CareerChoiceObject.PassiveEffect(-15, PassiveEffectType.AccuracyPenalty, true));
            Passive("RicochetTactics", 4, "Always ricochet; 30% detonate; buckshot +3.", SpecialPassive());

            Passive("IncendiaryFuses", 1, "Barrage: +10% damage, ignite, charge.", SpecialPassive());
            Passive("IncendiaryFuses", 2, "Barrage burn: +2s, double stack.", SpecialPassive());
            Passive("IncendiaryFuses", 3, "Barrage cooldown -5s.", SpecialPassive());
            Passive("IncendiaryFuses", 4, "+30% Fire damage.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Damage, new DamageProportionTuple(DamageType.Fire, 30), AttackTypeMask.Ranged));

            Passive("GrandBattery", 1, "Barrage: +2 shells.", SpecialPassive());
            Passive("GrandBattery", 2, "Barrage: +10% damage.", SpecialPassive());
            Passive("GrandBattery", 3, "Barrage: larger impact radius.", SpecialPassive());
            Passive("GrandBattery", 4, "Barrage: +2 shells, -5s cooldown.", SpecialPassive());
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
