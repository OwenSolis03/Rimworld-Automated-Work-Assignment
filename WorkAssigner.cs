using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VSE.Passions;

namespace Automated_Work_Assignment 
{
    public static class WorkAssigner
    {
        private const float PassionBurningBonus = 10f; // Usado solo si VSE NO está activo
        private const float PassionInterestedBonus = 5f; // Usado solo si VSE NO está activo
        private const int DefaultPriority = 0;

        private struct PawnSuitability { public Pawn pawn; public float score; }

        public static void RefreshAssignments()
        {
            AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>().GetSettings<AutomatedWorkSettings>();
            if (!settings.modEnabled) { return; }


            List<WorkTypeDef> workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd.workTags != WorkTags.None).ToList();

            List<Pawn> colonists = GetEligibleColonists(settings);
            if (Find.CurrentMap == null || !colonists.Any())
            {
                return;
            }

            foreach (WorkTypeDef workType in workTypesToManage)
            {
                WorkSettingValues workSetting = settings.GetWorkSetting(workType.defName);
                int desiredCount = workSetting.count;
                int targetPriority = workSetting.priority;

                if (desiredCount > 0)
                {
                    AssignWorkPriorities(workType, desiredCount, targetPriority, colonists);
                }
                else
                {
                    foreach (Pawn pawn in colonists) {
                        if (pawn?.workSettings != null) {
                            pawn.workSettings.SetPriority(workType, 0);
                        }
                    }
                }
            }


            MainButtonDef workButtonDef = DefDatabase<MainButtonDef>.GetNamed("Work", false);
            if (workButtonDef == null) { Log.ErrorOnce("[AutoWork] Could not find MainButtonDef named 'Work' in DefDatabase.", 918273645); } // ErrorOnce para evitar spam



        }

        private static List<Pawn> GetEligibleColonists(AutomatedWorkSettings settings)
        {
            List<string> excludedIDs = settings?.excludedPawnIDs ?? new List<string>();
            if (Find.CurrentMap == null) return new List<Pawn>();

            return Find.CurrentMap.mapPawns.FreeColonists
                .Where(p => p != null && p.Spawned && !p.Downed && p.MentalStateDef == null && p.workSettings != null
                            && !excludedIDs.Contains(p.ThingID))
                .ToList();
        }

        private static float CalculateSuitability(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType))
            {
                return -1f;
            }

            float score = 0f;
            SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
            SkillRecord skill = null;

            if (relevantSkillDef != null)
            {
                skill = pawn.skills.GetSkill(relevantSkillDef);
                if (skill != null)
                {
                    score += skill.Level; 
                }

            }
            else
            {
                score = 1f;
            }


            float passionBonus = 0f;
            if (skill != null)
            {
                Passion passionValue = skill.passion;

                if (ModDetector.VSEIsActive) 
                {
                    try
                    {
                        PassionDef vsePassionDef = PassionManager.PassionToDef(passionValue);
                        if (vsePassionDef != null)
                        {

                            float calculatedBonus = (vsePassionDef.learnRateFactor - 1.0f) * 10f; // Ajusta '10f' si es necesario

                            passionBonus = Mathf.Max(0f, calculatedBonus);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorOnce($"[AutoWork] Error getting VSE passion for {pawn.LabelShortCap}. Skill {skill.def.defName}. Setting passion bonus to 0 for this pawn/skill. Exception: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 1984); // Log de error único por peón/skill
                        passionBonus = 0f; 
                    }
                }
                else
                {

                    passionBonus = passionValue == Passion.Major ? PassionBurningBonus : (passionValue == Passion.Minor ? PassionInterestedBonus : 0f);
                }
            }

            score += passionBonus; 


            if (score < 1f && relevantSkillDef != null)
            {
                score = 1f;
            }
            
            return score;
        }


        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, List<Pawn> colonists)
        {
#if Degub
            Log.Message($"[AutoWork Debug] AssignWorkPriorities START for '{workType.defName}'. DesiredCount={desiredCount}, TargetPriority={targetPriority}"); // <-- LOG
#endif
            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();


            foreach (Pawn pawn in colonists)
            {
                if (pawn?.workSettings == null) continue; 

                float score = CalculateSuitability(pawn, workType);
                if (score >= 0) 
                {
                    suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score });
                } else {

                    pawn.workSettings.SetPriority(workType, 0);
                }
            }
            
            suitabilityList.Sort((a, b) => b.score.CompareTo(a.score));

#if DEBUG
            Log.Message($"[AutoWork Debug] Suitability list for '{workType.defName}' count: {suitabilityList.Count}. Top scores:"); // <-- LOG
#endif
            
            for(int k=0; k < Mathf.Min(5, suitabilityList.Count); k++) {
                Log.Message($"  #{k+1}: {suitabilityList[k].pawn.LabelShortCap} (Score: {suitabilityList[k].score:F1})"); // <-- LOG
            }

            
            int priorityToAssign = targetPriority;
            if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter) { priorityToAssign = 1; }
            if (priorityToAssign < 1) priorityToAssign = 1;
            if (priorityToAssign > 4) priorityToAssign = 4;

#if Degub
            Log.Message($"[AutoWork Debug] Final priorityToAssign for '{workType.defName}' = {priorityToAssign}"); // <-- LOG
#endif
            
            HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
#if Degub
            Log.Message($"[AutoWork Debug] Assigning top {desiredCount} pawns for '{workType.defName}': (Loop condition: i < {suitabilityList.Count} && i < {desiredCount})"); // <-- LOG
#endif
            for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++)
            {
                Pawn pawnToAssign = suitabilityList[i].pawn;
#if Degub
                Log.Message($"  LOOP i={i}: Assigning {pawnToAssign.LabelShortCap} (Rank {i+1}) -> Priority {priorityToAssign}"); // <-- LOG
#endif
                pawnToAssign.workSettings.SetPriority(workType, priorityToAssign);
                assignedPawns.Add(pawnToAssign);
            }

#if Degub
            Log.Message($"[AutoWork Debug] Setting default priority ({DefaultPriority}) for remaining {suitabilityList.Count - assignedPawns.Count} suitable pawns in '{workType.defName}':"); // <-- LOG
#endif
            foreach(var suitability in suitabilityList)
            {
                if(!assignedPawns.Contains(suitability.pawn))
                {
#if Degub
                    Log.Message($"  Setting {suitability.pawn.LabelShortCap} -> Priority {DefaultPriority}"); // <-- LOG
#endif
                    suitability.pawn.workSettings.SetPriority(workType, DefaultPriority);
                }
            }
            
#if Degub
            Log.Message($"[AutoWork Debug] AssignWorkPriorities END for '{workType.defName}'."); // <-- LOG
#endif
        }
    }
}