using System;
using System.IO;
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
            SafePatch(typeof(TORCareersConstructorPatch));
            SafePatch(typeof(TORCareerChoiceGroupsConstructorPatch));
            SafePatch(typeof(TORCareerChoicesConstructorPatch));
            SafePatch(typeof(EngineerCharacterCreationOptionsPatch));
            SafePatch(typeof(EngineerCharacterCreationApplyProfessionBonusesPatch));
            SafePatch(typeof(EngineerCareerUIPatch));
            SafePatch(typeof(EngineerRangedStaggerImmunityPatch));
            SafePatch(typeof(EngineerAgentStatsPatch));
            SafePatch(typeof(EngineerGrenadeAmmoPatch));
            SafePatch(typeof(EngineerBuckshotPatch));
            SafePatch(typeof(EngineerGrenadeExplosionPatch));
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            mission.AddMissionBehavior(new EngineerCareerMissionLogic());
        }

        private void SafePatch(Type patchType)
        {
            try
            {
                var patchedMethods = _harmony.CreateClassProcessor(patchType).Patch();
                Log($"Patched {patchType.Name}: {patchedMethods?.Count ?? 0} method(s).");
            }
            catch (Exception ex)
            {
                Log($"Failed to patch {patchType.FullName}: {ex}");
            }
        }

        internal static void Log(string message)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Tor",
                    "TOR_EngineerCareer.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never make the game startup path more fragile.
            }
        }
    }
}
