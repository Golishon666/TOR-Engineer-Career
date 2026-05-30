using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TOR_Core.CampaignMechanics.CharacterCreation;
using TOR_Core.CharacterDevelopment;
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
    }

    [HarmonyPatch(typeof(TORCharacterCreationContentHandler), MethodType.Constructor)]
    internal static class TORCharacterCreationContentHandlerConstructorPatch
    {
        private static bool Prepare()
        {
            return AccessTools.Constructor(typeof(TORCharacterCreationContentHandler), Type.EmptyTypes) != null;
        }

        private static void Postfix(TORCharacterCreationContentHandler __instance)
        {
            var options = Traverse.Create(__instance).Field<List<CharacterCreationOption>>("_options").Value;
            if (options == null || options.Any(x => x.Id == EngineerCharacterCreation.ProfessionId))
            {
                return;
            }

            options.Add(EngineerCharacterCreation.CreateProfessionOption());
        }
    }

    [HarmonyPatch(typeof(TORCharacterCreationContentHandler), "ApplyProfessionBonuses")]
    internal static class TORCharacterCreationApplyProfessionBonusesPatch
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
}
