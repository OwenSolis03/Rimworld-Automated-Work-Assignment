using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment
{
    public static class WorkAssigner
    {
        private const float PassionBurningBonus = 10f;
        private const float PassionInterestedBonus = 5f;
        private const int DefaultPriority = 0;

        private struct PawnSuitability { public Pawn pawn; public float score; }

        public static void RefreshAssignments()
        {
            AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>().GetSettings<AutomatedWorkSettings>();
            if (!settings.modEnabled) { return; }

            Log.Message("[AutoWork] Starting work assignment refresh...");

            List<WorkTypeDef> workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd.workTags != WorkTags.None).ToList();

            List<Pawn> colonists = GetEligibleColonists(settings);
            if (Find.CurrentMap == null || !colonists.Any()) {
                Log.Warning("[AutoWork] No eligible colonists found or map not loaded. Skipping refresh.");
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
            if (workButtonDef == null) { Log.Error("[AutoWork] Could not find MainButtonDef named 'Work' in DefDatabase."); }


            Log.Message("[AutoWork] Work assignment refresh complete.");
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
            if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) return -1f;
            float score = 0f;
            SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
            if (relevantSkillDef != null) {
                SkillRecord skill = pawn.skills.GetSkill(relevantSkillDef);
                if (skill != null) {
                    score += skill.Level;
                    score += skill.passion == Passion.Major ? PassionBurningBonus : (skill.passion == Passion.Minor ? PassionInterestedBonus : 0f);
                }
            } else { score = 1f; } 
            return score;
        }



        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, List<Pawn> colonists)
        {
            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
            foreach (Pawn pawn in colonists) {
                if (pawn?.workSettings == null) continue;
                float score = CalculateSuitability(pawn, workType);
                if (score >= 0) {
                    suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score });
                } else {
                    pawn.workSettings.SetPriority(workType, 0);
                }
            }

            suitabilityList.Sort((a, b) => b.score.CompareTo(a.score));


            int priorityToAssign = targetPriority;

            if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter)
            {
                priorityToAssign = 1;
            }

            if (priorityToAssign < 1) priorityToAssign = 1;
            if (priorityToAssign > 4) priorityToAssign = 4;


            HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
            for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++) {
                Pawn pawnToAssign = suitabilityList[i].pawn;
                pawnToAssign.workSettings.SetPriority(workType, priorityToAssign); 
                assignedPawns.Add(pawnToAssign);
            }

            foreach(var suitability in suitabilityList) {
                if(!assignedPawns.Contains(suitability.pawn)) {
                    suitability.pawn.workSettings.SetPriority(workType, DefaultPriority); 
                }
            }
        }
    }
}