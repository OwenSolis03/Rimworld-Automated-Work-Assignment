using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Static class containing the core logic for calculating pawn suitability
    /// and assigning work priorities based on per-save settings.
    /// </summary>
    public static class WorkAssigner
    {
        // --- Constants ---
        /// <summary> Bonus score added for Major Passion (Burning). </summary>
        private const float PassionBurningBonus = 10f;
        /// <summary> Bonus score added for Minor Passion (Interested). </summary>
        private const float PassionInterestedBonus = 5f;
        /// <summary> The default priority value (0) assigned to pawns not selected for a specific priority. </summary>
        private const int DefaultPriority = 0;

        /// <summary> Helper struct for sorting. </summary>
        private struct PawnSuitability { public Pawn pawn; public float score; }

        /// <summary>
        /// Main entry point for refreshing work assignments.
        /// Now uses per-save settings obtained via AutomatedWorkAssignmentMod.CurrentData.
        /// </summary>
        public static void RefreshAssignments()
        {
            // --- Get Per-Save Data ---
            AutomatedWork_SaveData saveData = null;
            try
            {
                saveData = AutomatedWorkAssignmentMod.CurrentData;
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception retrieving save data in RefreshAssignments: {ex}");
                return;
            }

            if (saveData == null)
            {
                Log.ErrorOnce("[AutoWork] Per-save data (AutomatedWork_SaveData) is null in RefreshAssignments.", 1984775);
                return;
            }
            if (!saveData.modEnabled)
            {
                return;
            }

            List<WorkTypeDef> workTypesToManage = null;
            List<Pawn> colonists = null;

            try
            {
                workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                    .ToList();
                colonists = GetEligibleColonists(saveData);
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception retrieving WorkTypeDefs or Colonists: {ex}"); return; }

            if (Find.CurrentMap == null || colonists == null || !colonists.Any()) return;

            // --- Calculate total eligible count once for percentage calculations ---
            int totalEligibleColonists = colonists.Count;
            // -------------------------------------------------------------------

            foreach (WorkTypeDef workType in workTypesToManage)
            {
                try
                {
                    if (workType == null) continue;

                    if (saveData.excludedWorkTypeDefNames != null && saveData.excludedWorkTypeDefNames.Contains(workType.defName))
                    {
                        continue;
                    }

                    WorkSettingValues workSetting = saveData.GetWorkSetting(workType.defName);
                    if (workSetting == null) { Log.ErrorOnce($"[AutoWork] Null workSetting for {workType.defName}", workType.defName.GetHashCode() ^ 1); continue; }

                    int finalDesiredCount;

                    // --- Determine desired count based on mode ---
                    if (workSetting.usePercentage)
                    {
                        finalDesiredCount = CalculateCountFromPercentage(workSetting.percentage, totalEligibleColonists);
                    }
                    else
                    {
                        finalDesiredCount = workSetting.count;
                    }
                    finalDesiredCount = Mathf.Min(finalDesiredCount, totalEligibleColonists);
                    // ---------------------------------------------

                    int targetPriority = workSetting.priority;

                    if (finalDesiredCount > 0)
                    {
                        AssignWorkPriorities(workType, finalDesiredCount, targetPriority, colonists);
                    }
                    else
                    {
                        foreach (Pawn pawn in colonists)
                        {
                            pawn?.workSettings?.SetPriority(workType, DefaultPriority);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing WorkTypeDef '{workType?.defName ?? "NULL"}' in RefreshAssignments: {ex}");
                }
            }

            // --- Optional check for MainButtonDef ---
            try { if (DefDatabase<MainButtonDef>.GetNamed("Work", false) == null) { Log.ErrorOnce("[AutoWork] Could not find MainButtonDef 'Work'.", 918273645); } }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception checking MainButtonDef: {ex}"); }

        }

        /// <summary>
        /// Calculates the integer number of pawns corresponding to a percentage.
        /// </summary>
        private static int CalculateCountFromPercentage(float percentage, int totalEligibleCount)
        {
            if (percentage <= 0f) return 0;
            if (percentage >= 1f) return totalEligibleCount;
            return Mathf.RoundToInt(percentage * totalEligibleCount);
        }

        /// <summary>
        /// Gets a list of colonists eligible for automatic work assignment in the current save.
        /// </summary>
        /// <param name="saveData">The current save game's data component containing the exclusion list.</param>
        /// <returns>A list of eligible Pawn objects.</returns>
        internal static List<Pawn> GetEligibleColonists(AutomatedWork_SaveData saveData)
        {
            List<string> excludedIDs = saveData?.excludedPawnIDs ?? new List<string>();
            if (Find.CurrentMap == null) return new List<Pawn>();

            try
            {
                return Find.CurrentMap.mapPawns.FreeColonists
                    .Where(p => p != null
                                && p.Spawned
                                && !p.Downed
                                && p.MentalStateDef == null
                                && p.workSettings != null
                                && !excludedIDs.Contains(p.ThingID))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception in GetEligibleColonists: {ex}");
                return new List<Pawn>();
            }
        }

        /// <summary>
        /// Helper method to get the count of eligible colonists efficiently for the current save.
        /// </summary>
        /// <param name="saveData">The current save game's data component.</param>
        internal static int GetEligibleColonistCount(AutomatedWork_SaveData saveData)
        {
            if (saveData == null) return 0;
            return GetEligibleColonists(saveData).Count;
        }


        /// <summary>
        /// Calculates suitability score.
        /// </summary>
        private static float CalculateSuitability(Pawn pawn, WorkTypeDef workType)
        {
            try
            {
                if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) return -1f;

                float score = 0f;
                SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                SkillRecord skill = null;
                if (relevantSkillDef != null) { skill = pawn.skills.GetSkill(relevantSkillDef); score = skill != null ? skill.Level : 1f; } else { score = 1f; }

                float passionBonus = 0f;
                if (skill != null) {
                    Passion passionValue = skill.passion;
                    if (ModDetector.VSEIsActive) {
                        ModDetector.EnsureReflectionInitialized();
                        if (ModDetector.VSEReflectionSuccess && ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorField != null) {
                            try {
                                object passionDefObj = ModDetector.VSE_PassionToDefMethod.Invoke(null, new object[] { passionValue });
                                if (passionDefObj != null) {
                                    object learnFactorObj = ModDetector.VSE_LearnRateFactorField.GetValue(passionDefObj);
                                    if (learnFactorObj is float learnRateFactor) { passionBonus = Mathf.Max(0f, (learnRateFactor - 1.0f) * 10f); }
                                }
                            } catch (Exception ex) { Log.ErrorOnce($"[AutoWork Compat] Exception VSE invoke/get {pawn.LabelShortCap},{skill.def.defName}: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 2028); passionBonus = 0f; }
                        }
                    }
                    if (!ModDetector.VSEIsActive || !ModDetector.VSEReflectionSuccess) { passionBonus = passionValue == Passion.Major ? PassionBurningBonus : (passionValue == Passion.Minor ? PassionInterestedBonus : 0f); passionBonus = Mathf.Max(0f, passionBonus); }
                }
                score += passionBonus;
                if (score < 1f && relevantSkillDef != null) score = 1f;
                return score;
            } catch (Exception ex) { Log.Error($"[AutoWork] Exception CalculateSuitability {pawn?.ThingID ?? "NULL"},{workType?.defName ?? "NULL"}: {ex}"); return -1f; }
        }


        /// <summary>
        /// Assigns priorities.
        /// </summary>
        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, List<Pawn> colonists)
        {
            if (workType == null || colonists == null) return;
            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
            try {
                foreach (Pawn pawn in colonists) {
                    if (pawn?.workSettings == null) continue;
                    float score = CalculateSuitability(pawn, workType);
                    if (score >= 0) { suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score }); }
                    else { pawn.workSettings.SetPriority(workType, DefaultPriority); }
                }
            } catch (Exception ex) { Log.Error($"[AutoWork] Exception calculating suitability list {workType.defName}: {ex}"); return; }

            try { suitabilityList.Sort((a, b) => b.score.CompareTo(a.score)); }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception sorting suitability list {workType.defName}: {ex}"); return; }

            int priorityToAssign = targetPriority;
            if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter) priorityToAssign = 1;
            if (priorityToAssign < 1) priorityToAssign = 1;
            if (priorityToAssign > 4) priorityToAssign = 4;

            try {
                HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
                for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++) {
                    Pawn pawnToAssign = suitabilityList[i].pawn;
                    pawnToAssign?.workSettings?.SetPriority(workType, priorityToAssign);
                    if (pawnToAssign != null) assignedPawns.Add(pawnToAssign);
                }
                foreach (var suitability in suitabilityList) {
                    if (suitability.pawn != null && !assignedPawns.Contains(suitability.pawn)) {
                        suitability.pawn.workSettings?.SetPriority(workType, DefaultPriority);
                    }
                }
            } catch (Exception ex) { Log.Error($"[AutoWork] Exception assigning priorities {workType.defName}: {ex}"); }
        }

    }
}