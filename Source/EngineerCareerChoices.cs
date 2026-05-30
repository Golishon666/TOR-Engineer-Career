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
        }

        protected override void InitializeKeyStones()
        {
            _engineerRoot.Initialize(CareerID,
                $"Open Fire! orders a brutal gunline volley. While active, the Engineer gains ranged physical damage and reload speed. {EngineerCareerHelper.BuildOpenFireDurationText()}",
                null,
                true,
                ChoiceType.Keystone,
                CombineMutations(
                    RenameOpenFire(),
                    MutateLetThemHaveIt("let_them_have_it_range_dmg", "let_them_have_it_melee_rls")));

            Keystone("PowderDrill",
                "Open Fire! lasts longer, grants stronger reload speed, and scales with Gunpowder (100 Gunpowder = +10% reload and ranged damage).",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Duration), EngineerCareerHelper.PowderDrillDurationBonus),
                    MutateStatusAdd("let_them_have_it_melee_rls", 0.10f),
                    MutateStatusSkillScale("let_them_have_it_melee_rls", TORSkills.GunPowder, EngineerCareerHelper.GunPowderEffectScale),
                    MutateStatusSkillScale("let_them_have_it_range_dmg", TORSkills.GunPowder, EngineerCareerHelper.GunPowderEffectScale)));

            Keystone("FieldTesting",
                "Open Fire! also grants ranged physical resistance and scales with Athletics (+0.03s duration and +0.05% resistance per Athletics level).",
                CombineMutations(
                    MutateLetThemHaveIt("let_them_have_it_range_res"),
                    MutateAbilitySkillScale(nameof(AbilityTemplate.Duration), DefaultSkills.Athletics, EngineerCareerHelper.AthleticsDurationScale),
                    MutateStatusSkillScale("let_them_have_it_range_res", DefaultSkills.Athletics, 0.0005f)));

            Keystone("ExplosiveRounds",
                "Unlocks explosive bullets. Open Fire! increases the blast radius of explosive bullets.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Radius), 0.5f),
                    MutateLetThemHaveIt("let_them_have_it_range_dmg")));

            Keystone("SuppressionFire",
                "Open Fire! affects a wider area and also grants ranged physical resistance.",
                CombineMutations(
                    MutateTriggeredEffectFloat("apply_let_them_have_it", nameof(TriggeredEffectTemplate.Radius), 2f),
                    MutateLetThemHaveIt("let_them_have_it_range_res")));

            Keystone("Grenadier",
                "Under Open Fire!, a grenade that kills 15 enemies at once is refunded. Open Fire! scales with Throwing (100 Throwing = +10%).",
                CombineMutations(
                    MutateStatusSkillScale("let_them_have_it_range_dmg", DefaultSkills.Throwing, EngineerCareerHelper.ThrowingEffectScale),
                    MutateStatusSkillScale("let_them_have_it_melee_rls", DefaultSkills.Throwing, EngineerCareerHelper.ThrowingEffectScale)));

            Keystone("PiercingDoctrine",
                "Unlocks overpenetrating bullets. Open Fire! further increases ranged physical damage.",
                MutateStatusAdd("let_them_have_it_range_dmg", 0.15f));

            Keystone("RicochetTactics",
                "During Open Fire!, firearm hits ricochet into nearby enemies. Each keystone talent adds another ricochet.",
                CombineMutations(
                    MutateAbilityFloat(nameof(AbilityTemplate.Duration), EngineerCareerHelper.RicochetTacticsDurationBonus),
                    MutateTriggeredEffectFloat("apply_let_them_have_it", nameof(TriggeredEffectTemplate.Radius), 2f)));
        }

        protected override void InitializePassives()
        {
            Passive("PowderDrill", 1, "+6 extra ammo per ammunition pouch.", Ammo());
            Passive("PowderDrill", 2, "+10% personal gunpowder ranged physical damage.", GunpowderDamage(10));
            Passive("PowderDrill", 3, "+15% personal firearm accuracy.", new CareerChoiceObject.PassiveEffect(-15, PassiveEffectType.AccuracyPenalty, true));
            Passive("PowderDrill", 4, "Ranged troops in your party gain +25 Gunpowder skill.", new CareerChoiceObject.PassiveEffect(25, new List<string> { nameof(TORSkills.GunPowder) }, IsRangedTroop));

            Passive("FieldTesting", 1, "+6 extra ammo per ammunition pouch.", Ammo());
            Passive("FieldTesting", 2, "+15% personal ranged physical resistance.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Resistance, new DamageProportionTuple(DamageType.Physical, 15), AttackTypeMask.Ranged));
            Passive("FieldTesting", 3, "+15% personal firearm projectile speed.", SpecialPassive());
            Passive("FieldTesting", 4, "+1 party movement speed on the campaign map.", new CareerChoiceObject.PassiveEffect(1f, PassiveEffectType.PartyMovementSpeed));

            Passive("ExplosiveRounds", 1, "+6 extra ammo per ammunition pouch.", Ammo());
            Passive("ExplosiveRounds", 2, "You are no longer staggered by ranged damage.", SpecialPassive());
            Passive("ExplosiveRounds", 3, "+10% ranged physical damage for ranged troops.", TroopRangedDamage(10));
            Passive("ExplosiveRounds", 4, "+10% personal ranged armor penetration.", new CareerChoiceObject.PassiveEffect(-10, PassiveEffectType.ArmorPenetration, AttackTypeMask.Ranged));

            Passive("SuppressionFire", 1, "+6 extra ammo per ammunition pouch.", Ammo());
            Passive("SuppressionFire", 2, "+10% ranged physical resistance for ranged troops.", TroopRangedResistance(10));
            Passive("SuppressionFire", 3, "+10% personal gunpowder ranged physical damage.", GunpowderDamage(10));
            Passive("SuppressionFire", 4, "+10 party size for the Engineer's gunline.", new CareerChoiceObject.PassiveEffect(10, PassiveEffectType.PartySize));

            Passive("Grenadier", 1, "+3 extra grenades per grenade pouch.", new CareerChoiceObject.PassiveEffect(3, PassiveEffectType.Special, false));
            Passive("Grenadier", 2, "+30% grenade explosion radius.", SpecialPassive());
            Passive("Grenadier", 3, "+30% personal Fire damage.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Damage, new DamageProportionTuple(DamageType.Fire, 30), AttackTypeMask.Ranged));
            Passive("Grenadier", 4, "+30% grenade throw speed.", SpecialPassive());

            Passive("PiercingDoctrine", 1, "+6 extra ammo per ammunition pouch.", Ammo());
            Passive("PiercingDoctrine", 2, "+25% personal ranged armor penetration.", new CareerChoiceObject.PassiveEffect(-25, PassiveEffectType.ArmorPenetration, AttackTypeMask.Ranged));
            Passive("PiercingDoctrine", 3, "+20% personal gunpowder ranged physical damage.", GunpowderDamage(20));
            Passive("PiercingDoctrine", 4, "+15% ranged physical damage for ranged troops.", TroopRangedDamage(15));

            Passive("RicochetTactics", 1, "+6 extra ammo per ammunition pouch.", Ammo());
            Passive("RicochetTactics", 2, "+15% personal ranged physical resistance.", new CareerChoiceObject.PassiveEffect(PassiveEffectType.Resistance, new DamageProportionTuple(DamageType.Physical, 15), AttackTypeMask.Ranged));
            Passive("RicochetTactics", 3, "+15% personal firearm accuracy.", new CareerChoiceObject.PassiveEffect(-15, PassiveEffectType.AccuracyPenalty, true));
            Passive("RicochetTactics", 4, "Firearm hits ricochet once even without Open Fire!. Buckshot fires +3 extra pellets.", SpecialPassive());
        }

        private static CareerChoiceObject Register(string id)
        {
            return Campaign.Current.ObjectManager.RegisterPresumedObject(new CareerChoiceObject(id));
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
            _choices[groupId + "Keystone"].Initialize(CareerID, description, groupId, false, ChoiceType.Keystone, mutations);
        }

        private void Passive(string groupId, int index, string description, CareerChoiceObject.PassiveEffect passive)
        {
            _choices[groupId + "Passive" + index].Initialize(CareerID, description, groupId, false, ChoiceType.Passive, null, passive);
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
                    PropertyName = nameof(AbilityTemplate.TooltipDescription),
                    MutationType = OperationType.Replace,
                    PropertyValue = (choice, originalValue, agent) =>
                        $"Order a disciplined volley. While Open Fire! is active, the Engineer holds the line and gains ranged physical damage and reload speed. {EngineerCareerHelper.BuildOpenFireDurationText()} Career talents can extend the order, add ranged protection, explosive rounds, piercing shots, ricochets and grenadier tricks."
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
            return new List<CareerChoiceObject.MutationObject>
            {
                new()
                {
                    MutationTargetType = typeof(StatusEffectTemplate),
                    MutationTargetOriginalId = statusId,
                    PropertyName = nameof(StatusEffectTemplate.BaseEffectValue),
                    MutationType = OperationType.Add,
                    PropertyValue = (choice, originalValue, agent) =>
                        CareerHelper.AddSkillEffectToValue(choice, agent, new List<SkillObject> { skill }, scale)
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

        private static List<CareerChoiceObject.MutationObject> MutateAbilitySkillScale(string propertyName, SkillObject skill, float scale)
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
                        CareerHelper.AddSkillEffectToValue(choice, agent, new List<SkillObject> { skill }, scale)
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
    }
}
