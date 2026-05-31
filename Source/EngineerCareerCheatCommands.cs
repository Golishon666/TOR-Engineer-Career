using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TOR_Core.CampaignMechanics.CustomResources;
using TOR_Core.CampaignSupport.TownBehaviours;
using TOR_Core.CharacterDevelopment;
using TOR_Core.Extensions;
using TOR_Core.Quests;

namespace TOR_EngineerCareer
{
    public static class EngineerCareerCheatCommands
    {
        private const string DefaultPrestigeAmount = "500";

        [CommandLineFunctionality.CommandLineArgumentFunction("add_prestige", "tor_engineer")]
        public static string AddPrestige(List<string> arguments)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            if (CampaignCheats.CheckHelp(arguments))
            {
                return "Usage: tor_engineer.add_prestige [amount]\nAdds Prestige to the main hero (default: 500).\n";
            }

            var amountText = arguments != null && arguments.Count > 0 ? arguments[0] : DefaultPrestigeAmount;
            if (!int.TryParse(amountText, out var amount) || amount <= 0)
            {
                return "Amount must be a positive integer.\nUsage: tor_engineer.add_prestige [amount]\n";
            }

            var resource = CustomResourceManager.GetResourceObject("Prestige");
            if (resource == null)
            {
                return "Prestige resource not found.\n";
            }

            Hero.MainHero.AddCustomResource("Prestige", amount);
            var total = Hero.MainHero.GetCustomResourceValue("Prestige");
            return $"Added {amount} Prestige. Current total: {total}.\n";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("complete_quest", "tor_engineer")]
        public static string CompleteEngineerQuest(List<string> arguments)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            if (CampaignCheats.CheckHelp(arguments))
            {
                return "Usage: tor_engineer.complete_quest\n"
                       + "Completes the Master Engineer quest line and grants Engineer career + CanPlaceArtillery + AbilityUser.\n"
                       + "Also marks the Nuln engineer as knowing the player.\n";
            }

            if (Campaign.Current == null)
            {
                return "Function only available in campaign mode.\n";
            }

            PrepareMasterEngineerBehavior();
            var quest = EngineerQuest.GetCurrentActiveIfExists();
            if (quest != null)
            {
                AdvanceQuestToCompletion(quest);
            }

            EnsureEngineerCareer(Hero.MainHero);
            GrantEngineerQuestRewards(Hero.MainHero);
            EngineerCareerHelper.EnsureDefaultArtilleryStock(Hero.MainHero);

            return quest != null
                ? "Engineer quest completed. Engineer career, CanPlaceArtillery, AbilityUser and default artillery granted.\n"
                : "No active engineer quest found; Engineer career, quest rewards and default artillery granted directly.\n";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("unlock_dwarf", "tor_engineer")]
        public static string UnlockDwarfContraband(List<string> arguments)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            if (CampaignCheats.CheckHelp(arguments))
            {
                return "Usage: tor_engineer.unlock_dwarf [tier]\n"
                       + "Unlocks dwarf contraband shop tiers 1-3 without spending Prestige (default: all 3).\n";
            }

            var maxTier = EngineerDwarfContrabandCatalog.MaxTier;
            if (arguments != null && arguments.Count > 0)
            {
                if (!int.TryParse(arguments[0], out maxTier) ||
                    maxTier < 1 ||
                    maxTier > EngineerDwarfContrabandCatalog.MaxTier)
                {
                    return $"Tier must be between 1 and {EngineerDwarfContrabandCatalog.MaxTier}.\n";
                }
            }

            var hero = Hero.MainHero;
            for (var tier = 1; tier <= maxTier; tier++)
            {
                var attributeId = EngineerDwarfContrabandCatalog.GetUnlockAttributeId(tier);
                if (!hero.HasAttribute(attributeId))
                {
                    hero.AddAttribute(attributeId);
                }
            }

            return $"Unlocked dwarf contraband tiers 1-{maxTier}.\n";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("setup_test", "tor_engineer")]
        public static string SetupTest(List<string> arguments)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType))
            {
                return CampaignCheats.ErrorType;
            }

            if (CampaignCheats.CheckHelp(arguments))
            {
                return "Usage: tor_engineer.setup_test\n"
                       + "Grants Engineer career, 1500 Prestige, completes the master quest, and unlocks all dwarf contraband tiers.\n";
            }

            EnsureEngineerCareer(Hero.MainHero);
            Hero.MainHero.AddCustomResource("Prestige", 1500);
            PrepareMasterEngineerBehavior();

            var quest = EngineerQuest.GetCurrentActiveIfExists();
            if (quest != null)
            {
                AdvanceQuestToCompletion(quest);
            }

            GrantEngineerQuestRewards(Hero.MainHero);
            EngineerCareerHelper.EnsureDefaultArtilleryStock(Hero.MainHero);

            for (var tier = 1; tier <= EngineerDwarfContrabandCatalog.MaxTier; tier++)
            {
                var attributeId = EngineerDwarfContrabandCatalog.GetUnlockAttributeId(tier);
                if (!Hero.MainHero.HasAttribute(attributeId))
                {
                    Hero.MainHero.AddAttribute(attributeId);
                }
            }

            return "Test setup complete: Engineer career, 1500 Prestige, master quest rewards, 2 default artillery pieces, all contraband tiers.\n";
        }

        private static void PrepareMasterEngineerBehavior()
        {
            var behavior = Campaign.Current.GetCampaignBehavior<MasterEngineerTownBehaviour>();
            if (behavior == null)
            {
                return;
            }

            var traverse = Traverse.Create(behavior);
            traverse.Field<bool>("_knowsPlayer").Value = true;
            traverse.Field<bool>("_gaveQuestOffer").Value = true;

            var quest = EngineerQuest.GetCurrentActiveIfExists();
            if (quest != null)
            {
                traverse.Field<EngineerQuest>("RunawayPartsQuest").Value = quest;
            }
        }

        private static void AdvanceQuestToCompletion(EngineerQuest quest)
        {
            const int maxSteps = 8;
            var steps = 0;

            while (quest.IsOngoing && steps < maxSteps)
            {
                if (quest.GetCurrentProgress() >= (int)EngineerQuestStates.HandInRogueEngineerHunt)
                {
                    quest.UpdateProgressOnQuest();
                    break;
                }

                quest.UpdateProgressOnQuest(withProgress: true);
                steps++;
            }
        }

        private static void GrantEngineerQuestRewards(Hero hero)
        {
            if (!hero.HasAttribute("AbilityUser"))
            {
                hero.AddAttribute("AbilityUser");
            }

            if (!hero.HasAttribute("CanPlaceArtillery"))
            {
                hero.AddAttribute("CanPlaceArtillery");
            }

            hero.AddSkillXp(TORSkills.GunPowder, 250f);
        }

        private static void EnsureEngineerCareer(Hero hero)
        {
            if (EngineerCareerHelper.IsEngineerHero(hero))
            {
                return;
            }

            var career = TORCareers.All.FirstOrDefault(x => x.StringId == EngineerCareerRegistry.CareerId);
            if (career != null)
            {
                hero.AddCareer(career);
            }
        }
    }
}
