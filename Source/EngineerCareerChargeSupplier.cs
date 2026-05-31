using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TOR_Core.AbilitySystem;
using TOR_Core.BattleMechanics.DamageSystem;
using TOR_Core.CharacterDevelopment.CareerSystem;
using TOR_Core.Extensions;
using TOR_Core.Extensions.ExtendedInfoSystem;

namespace TOR_EngineerCareer
{
    internal static class EngineerCareerChargeSupplier
    {
        public static float SupplyCharge(
            Agent affectingAgent,
            Agent affectedAgent,
            ChargeType chargeType,
            int chargeValue,
            AttackTypeMask mask = AttackTypeMask.Melee,
            CareerHelper.ChargeCollisionFlag collisionFlag = CareerHelper.ChargeCollisionFlag.None)
        {
            if (chargeType != ChargeType.DamageDone)
            {
                return 0f;
            }

            if (affectingAgent == null ||
                affectedAgent == null ||
                Agent.Main == null ||
                collisionFlag == CareerHelper.ChargeCollisionFlag.HitShield ||
                affectingAgent.Team == affectedAgent.Team ||
                affectingAgent.IsEnemyOf(Agent.Main) ||
                affectedAgent.Team == Agent.Main.Team)
            {
                return 0f;
            }

            if (!EngineerCareerHelper.IsEngineerMainAgent(affectingAgent) ||
                !IsGunpowderRangedHit(affectingAgent, mask))
            {
                return 0f;
            }

            var explainedNumber = new ExplainedNumber(chargeValue);
            explainedNumber.LimitMin(1);

            if (collisionFlag == CareerHelper.ChargeCollisionFlag.HeadShot &&
                EngineerCareerHelper.HasChoice("FieldTestingPassive3"))
            {
                explainedNumber.AddFactor(1f);
            }

            return explainedNumber.ResultNumber;
        }

        private static bool IsGunpowderRangedHit(Agent affectingAgent, AttackTypeMask mask)
        {
            if ((mask & AttackTypeMask.Ranged) == 0)
            {
                return false;
            }

            var weapon = affectingAgent.WieldedWeapon;
            return !weapon.IsEmpty &&
                   weapon.CurrentUsageItem != null &&
                   weapon.CurrentUsageItem.IsGunPowderWeapon();
        }
    }
}
