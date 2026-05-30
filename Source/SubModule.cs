using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TOR_EngineerCareer
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            _harmony = new Harmony("tor.engineer.career");
            _harmony.PatchAll();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            mission.AddMissionBehavior(new EngineerCareerMissionLogic());
        }
    }
}
