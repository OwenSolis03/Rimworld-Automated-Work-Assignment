using Verse;
using RimWorld;
using System;
using UnityEngine;

using VSE.Passions;

namespace Automated_Work_Assignment
{
    public static class VSECompat
    {
        private const float PassionBurningBonus = 10f;
        private const float PassionInterestedBonus = 5f;

        public static float GetPassionBonus_VSE(SkillRecord skill, Pawn pawn) 
        {

            if (skill == null || pawn == null) return 0f;

            Passion passionValue = skill.passion;
            float passionBonus = 0f;

            try
            {
                PassionDef vsePassionDef = PassionManager.PassionToDef(passionValue);

                if (vsePassionDef != null)
                {
                    float calculatedBonus = (vsePassionDef.learnRateFactor - 1.0f) * 10f;
                    passionBonus = Mathf.Max(0f, calculatedBonus);
                }
                else
                {
                    Log.Warning($"[AutoWork Compat] VSE Active but PassionManager.PassionToDef returned null for passion value {passionValue}. Applying default vanilla bonus for safety.");
                    passionBonus = passionValue == Passion.Major ? PassionBurningBonus : (passionValue == Passion.Minor ? PassionInterestedBonus : 0f);
                    passionBonus = Mathf.Max(0f, passionBonus); 
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[AutoWork Compat] Error in VSECompat.GetPassionBonus_VSE for {pawn.LabelShortCap ?? "???"} skill {skill.def.defName}. Setting passion bonus to 0. Exception: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 2025);
                passionBonus = 0f;
            }

            return passionBonus;
        }
    }
}