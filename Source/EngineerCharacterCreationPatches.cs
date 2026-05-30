using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TOR_Core.CampaignMechanics.CharacterCreation;
using TOR_Core.CharacterDevelopment;
using TOR_Core.CharacterDevelopment.CareerSystem;
using TOR_Core.Extensions;

namespace TOR_EngineerCareer
{
    internal static class EngineerCharacterCreation
    {
        public const string ProfessionId = "option_3_empire_engineer";
        public const string EquipmentRosterId = "tor_cc_empire_engineer_3";

        public static CharacterCreationOption CreateProfessionOption()
        {
            return new CharacterCreationOption
            {
                Id = ProfessionId,
                Culture = "empire",
                StageNumber = 3,
                EquipmentSetId = EquipmentRosterId,
                SkillsToIncrease = new[] { "Gunpowder", "Engineering", "Tactics", "Steward" },
                AttributeToIncrease = "Cunning",
                OptionText = "{=tor_engineer_cc_option}Imperial Engineer",
                PositiveEffectText = "{=tor_engineer_cc_effect}Engineer Career",
                OptionFlavourText = "{=tor_engineer_cc_flavour}You learned the sacred arithmetic of powder, bore and fuse in the Imperial gunnery schools. Now you take the field with rifle, shot and a doctrine simple enough for any soldier to understand: open fire."
            };
        }

        public static void RegisterProfessionOption(TORCharacterCreationContentHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            var options = Traverse.Create(handler).Field<List<CharacterCreationOption>>("_options").Value;
            if (options == null || options.Any(x => x.Id == ProfessionId))
            {
                return;
            }

            options.Add(CreateProfessionOption());
            SubModule.Log($"Registered character creation profession '{ProfessionId}'.");
        }
    }

    [HarmonyPatch(typeof(TORCharacterCreationContentHandler), nameof(TORCharacterCreationContentHandler.InitializeContent))]
    internal static class EngineerCharacterCreationOptionsPatch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(TORCharacterCreationContentHandler), nameof(TORCharacterCreationContentHandler.InitializeContent)) != null;
        }

        private static void Postfix(TORCharacterCreationContentHandler __instance)
        {
            EngineerCharacterCreation.RegisterProfessionOption(__instance);
        }
    }

    [HarmonyPatch(typeof(TORCharacterCreationContentHandler), "ApplyProfessionBonuses")]
    internal static class EngineerCharacterCreationApplyProfessionBonusesPatch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(TORCharacterCreationContentHandler), "ApplyProfessionBonuses") != null;
        }

        private static void Postfix(TORCharacterCreationContentHandler __instance)
        {
            if (__instance.GetSelectedProfessionId() != EngineerCharacterCreation.ProfessionId)
            {
                return;
            }

            var hero = Hero.MainHero;
            if (hero?.Culture?.StringId != "empire" || EngineerCareerRegistry.Engineer == null)
            {
                return;
            }

            hero.AddCareer(EngineerCareerRegistry.Engineer);
            var currentGunpowder = hero.GetSkillValue(TORSkills.GunPowder);
            hero.HeroDeveloper.SetInitialSkillLevel(TORSkills.GunPowder, Math.Max(currentGunpowder, 25));
        }
    }

    [HarmonyPatch(typeof(CareerObjectVM), MethodType.Constructor, new[] { typeof(CareerObject) })]
    internal static class EngineerCareerUIPatch
    {
        private static void Postfix(CareerObjectVM __instance, CareerObject career)
        {
            if (career?.StringId != EngineerCareerRegistry.CareerId)
            {
                return;
            }

            __instance.AbilityName = "Open Fire!";
            __instance.AbilitySpriteName = EngineerCareerHelper.OpenFireIconSprite;
        }
    }
}
