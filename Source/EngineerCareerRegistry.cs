using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TOR_Core.CampaignMechanics.Choices;
using TOR_Core.CharacterDevelopment;
using TOR_Core.CharacterDevelopment.CareerSystem;

namespace TOR_EngineerCareer
{
    internal static class EngineerCareerRegistry
    {
        public const string CareerId = "Engineer";
        public static CareerObject Engineer { get; private set; }

        public static void RegisterCareer()
        {
            if (Engineer != null)
            {
                return;
            }

            Engineer = Game.Current.ObjectManager.RegisterPresumedObject(new CareerObject(CareerId));
            Engineer.Initialize("Engineer", IsEligible, "LetThemHaveIt");
        }

        public static void RegisterChoiceGroups()
        {
            if (Engineer == null)
            {
                return;
            }

            RegisterGroup("PowderDrill", "Powder Drill", 1);
            RegisterGroup("FieldTesting", "Field Testing", 1);
            RegisterGroup("ExplosiveRounds", "Explosive Rounds", 2);
            RegisterGroup("SuppressionFire", "Suppression Fire", 2);
            RegisterGroup("Grenadier", "Grenadier", 2);
            RegisterGroup("PiercingDoctrine", "Piercing Doctrine", 3);
            RegisterGroup("RicochetTactics", "Ricochet Tactics", 3);
        }

        public static void RegisterChoices(TORCareerChoices choices)
        {
            if (Engineer == null)
            {
                return;
            }

            var allChoices = Traverse.Create(choices).Field<List<TORCareerChoicesBase>>("_allCareerChoices").Value;
            if (allChoices.All(x => x.GetID() != Engineer))
            {
                allChoices.Add(new EngineerCareerChoices(Engineer));
            }

            AddEngineerToCareerList();
        }

        private static void RegisterGroup(string id, string name, int tier)
        {
            if (MBObjectManager.Instance.GetObject<CareerChoiceGroupObject>(id) != null)
            {
                return;
            }

            var group = Game.Current.ObjectManager.RegisterPresumedObject(new CareerChoiceGroupObject(id));
            group.Initialize(name, Engineer, tier, AlwaysAvailable);
        }

        private static bool AlwaysAvailable(Hero hero, out string text)
        {
            text = string.Empty;
            return true;
        }

        private static bool IsEligible(Hero hero)
        {
            return hero?.Culture?.StringId == "empire";
        }

        private static void AddEngineerToCareerList()
        {
            var careers = TORCareers.All.ToList();
            if (careers.Any(x => x.StringId == CareerId))
            {
                return;
            }

            careers.Add(Engineer);
            Traverse.Create(TORCareers.Instance)
                .Field<MBReadOnlyList<CareerObject>>("_allCareers")
                .Value = new MBReadOnlyList<CareerObject>(careers);
        }
    }

    [HarmonyPatch(typeof(TORCareers), MethodType.Constructor)]
    internal static class TORCareersConstructorPatch
    {
        private static void Postfix()
        {
            EngineerCareerRegistry.RegisterCareer();
        }
    }

    [HarmonyPatch(typeof(TORCareerChoiceGroups), MethodType.Constructor)]
    internal static class TORCareerChoiceGroupsConstructorPatch
    {
        private static void Postfix()
        {
            EngineerCareerRegistry.RegisterChoiceGroups();
        }
    }

    [HarmonyPatch(typeof(TORCareerChoices), MethodType.Constructor)]
    internal static class TORCareerChoicesConstructorPatch
    {
        private static void Postfix(TORCareerChoices __instance)
        {
            EngineerCareerRegistry.RegisterChoices(__instance);
        }
    }
}
