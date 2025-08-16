using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Static class containing the core logic for calculating pawn suitability and assigning work priorities based on mod settings.
    /// </summary>
    public static class WorkAssigner
    {
        /// <summary>
        /// The default priority value (0) assigned to pawns for work types they are not assigned to.
        /// </summary>
        private const int DefaultPriority = 0;

        /// <summary>
        /// A private helper struct to temporarily hold a pawn, their calculated suitability score, and passion for sorting.
        /// </summary>
        private struct PawnSuitability { public Pawn pawn; public float score;
            public Passion passion;
        }
        
        /// <summary>
        /// The main entry point for the automatic work assignment logic. This method orchestrates the entire process
        /// of refreshing pawn work priorities based on the current mod settings for the active save game.
        /// It iterates through all manageable work types and applies either the simple or expert logic.
        /// This method can be called manually from the UI, regardless of the mod's enabled state.
        /// </summary>
        public static void RefreshAssignments()
        {
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
            
            int totalEligibleColonists = colonists.Count;

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
                    
                    if (workSetting.usePercentage)
                    {
                        finalDesiredCount = CalculateCountFromPercentage(workSetting.percentage, totalEligibleColonists);
                    }
                    else
                    {
                        finalDesiredCount = workSetting.count;
                    }
                    finalDesiredCount = Mathf.Min(finalDesiredCount, totalEligibleColonists);
                    
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

            try { if (DefDatabase<MainButtonDef>.GetNamed("Work", false) == null) { Log.ErrorOnce("[AutoWork] Could not find MainButtonDef 'Work'.", 918273645); } }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception checking MainButtonDef: {ex}"); }
        }

        /// <summary>
        /// Calculates the absolute number of pawns to assign based on a given percentage of the total eligible workforce.
        /// Ensures at least 1 pawn is returned if the percentage is greater than zero and there are eligible pawns.
        /// </summary>
        /// <param name="percentage">The desired percentage of the workforce, expressed as a float from 0.0 to 1.0.</param>
        /// <param name="totalEligibleCount">The total number of pawns available for assignment.</param>
        /// <returns>The calculated absolute number of pawns to assign.</returns>
        private static int CalculateCountFromPercentage(float percentage, int totalEligibleCount)
        {
            if (percentage <= 0f || totalEligibleCount <= 0) return 0;
            if (percentage >= 1f) return totalEligibleCount;
            float rawValue = percentage * totalEligibleCount;
            int calculatedCount = Mathf.RoundToInt(rawValue);
            
            return (calculatedCount == 0 && percentage > 0f) ? 1 : Mathf.Min(calculatedCount, totalEligibleCount);
        }

        /// <summary>
        /// Retrieves a list of all colonists who are eligible for automatic work assignment.
        /// </summary>
        /// <remarks>
        /// Eligibility is determined by several factors: the pawn must be a free colonist, spawned, not downed,
        /// not in a mental state, have work settings, and not be on the user-defined exclusion list.
        /// </remarks>
        /// <param name="saveData">The per-save data component containing the pawn exclusion list.</param>
        /// <returns>A list of eligible <see cref="Pawn"/> objects.</returns>
        public static List<Pawn> GetEligibleColonists(AutomatedWork_SaveData saveData)
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
        /// A convenience method to get the count of eligible colonists.
        /// </summary>
        /// <param name="saveData">The per-save data component.</param>
        /// <returns>The number of eligible pawns.</returns>
        internal static int GetEligibleColonistCount(AutomatedWork_SaveData saveData)
        {
            if (saveData == null) return 0;
            return GetEligibleColonists(saveData).Count;
        }

        /// <summary>
        /// Calculates a numerical 'suitability' score for a given pawn and work type. This score determines the pawn's ranking for assignment.
        /// </summary>
        /// <remarks>
        /// The scoring logic adapts based on active mods:
        /// 1. The base score is the pawn's skill level in the work type's relevant skill (or 1 if no skill is associated).
        /// 2. If Vanilla Skills Expanded (or a compatible mod) is active, it calculates an additive bonus based on the passion's `LearnRateFactor`.
        /// 3. If VSE is not active, it applies a multiplicative bonus for vanilla passions (1.25x for minor, 1.5x for major).
        /// 4. A small, random value (0.00-0.01) is added as a tie-breaker.
        /// Returns -1f for pawns who are incapable of the work type.
        /// </remarks>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <param name="workType">The work type to evaluate for.</param>
        /// <returns>The calculated suitability score, or -1f if ineligible.</returns>
        internal static float CalculateSuitability(Pawn pawn, WorkTypeDef workType)
        {
            try
            {
                if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) return -1f;

                float score;
                SkillRecord skill = null;
                SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                
                score = relevantSkillDef != null ? (skill = pawn.skills.GetSkill(relevantSkillDef))?.Level ?? 1f : 1f;

                if (skill != null) 
                {
                    if (ModDetector.VSEIsActive) {
                        ModDetector.EnsureReflectionInitialized();
                        if (ModDetector.VSEReflectionSuccess && ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorField != null) {
                            try {
                                object passionDefObj = ModDetector.VSE_PassionToDefMethod.Invoke(null, new object[] { skill.passion });
                                if (passionDefObj != null) {
                                    if (ModDetector.VSE_LearnRateFactorField.GetValue(passionDefObj) is float learnRateFactor) {
                                        score += Mathf.Max(0f, (learnRateFactor - 1.0f) * 10f);
                                    }
                                }
                            } catch (Exception ex) { Log.ErrorOnce($"[AutoWork Compat] Exception VSE invoke/get {pawn.LabelShortCap},{skill.def.defName}: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 2028); }
                        }
                    }
                    else
                    {
                        float passionMultiplier = 1.0f;
                        Passion passionValue = skill.passion;
                        if (passionValue == Passion.Major) passionMultiplier = 1.5f;
                        else if (passionValue == Passion.Minor) passionMultiplier = 1.25f;
                        score *= passionMultiplier;
                    }
                }
                
                if (score < 1f && relevantSkillDef != null) score = 1f;
                
                score += Rand.Range(0f, 0.01f);
                
                return score;
            } catch (Exception ex) { Log.Error($"[AutoWork] Exception CalculateSuitability {pawn?.ThingID ?? "NULL"},{workType?.defName ?? "NULL"}: {ex}"); return -1f; }
        }

        /// <summary>
        /// Assigns work priorities to the most suitable colonists for a specific work type, integrating simple and expert mode logic.
        /// </summary>
        /// <remarks>
        /// This method first calculates and sorts all eligible pawns by their suitability score for the given work type (using skill and passion).
        /// It then selects the top pawns based on the `desiredCount`. For each selected pawn, it determines the final priority:
        /// 1. It checks if an Expert Mode rule matches the pawn's skill level. If so, that rule's priority is used.
        /// 2. If no expert rule matches, it falls back to the `targetPriority` from the simple settings slider.
        /// 3. As a final override, if the work type is Doctor or Firefighter, the priority is forced to 1.
        /// Pawns who are not in the top selection are set to priority 0 for this work type.
        /// </remarks>
        /// <param name="workType">The work type being assigned.</param>
        /// <param name="desiredCount">The number of pawns to assign to this work type, determined by the simple settings.</param>
        /// <param name="targetPriority">The fallback priority from the simple settings slider.</param>
        /// <param name="colonists">The list of all eligible colonists to consider for assignment.</param>
        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, List<Pawn> colonists)
        {
            if (workType == null || colonists == null) return;
            
            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
            foreach (Pawn pawn in colonists) {
                if (pawn?.workSettings == null) continue;
                float score = CalculateSuitability(pawn, workType);
                if (score >= 0)
                {
                    Passion passion = Passion.None;
                    SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                    if (relevantSkillDef != null)
                    {
                        passion = pawn.skills.GetSkill(relevantSkillDef)?.passion ??  Passion.None;
                    }
                    suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score, passion = passion });
                }
                else { pawn.workSettings.SetPriority(workType, DefaultPriority); }
            }
            suitabilityList.Sort((a, b) =>
            {
                int scoreComparison = b.score.CompareTo(a.score);
                if (scoreComparison != 0) return scoreComparison;
                return b.passion.CompareTo(a.passion);
            });

            var expertManager = Current.Game.GetComponent<ExpertModeRuleManager>();
            bool rulesForThisWorkTypeExist = expertManager?.workTypeRules.ContainsKey(workType) == true && expertManager.workTypeRules[workType].Any();

            HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
            
            for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++)
            {
                Pawn pawnToAssign = suitabilityList[i].pawn;
                int priorityToAssign = targetPriority;

                if (rulesForThisWorkTypeExist)
                {
                    SkillDef relevantSkill = workType.relevantSkills?.FirstOrDefault();
                    int skillLevel = (relevantSkill != null) ? pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0 : 0;
                    
                    SkillPriorityRule matchingRule = expertManager.workTypeRules[workType]
                        .FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);
                    
                    if (matchingRule != null)
                    {
                        priorityToAssign = matchingRule.Priority;
                    }
                    else
                    {
                        priorityToAssign = DefaultPriority;
                    }
                }
                
                if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter)
                {
                    priorityToAssign = 1;
                }
                
                pawnToAssign?.workSettings?.SetPriority(workType, priorityToAssign);
                if (pawnToAssign != null) assignedPawns.Add(pawnToAssign);
            }
            
            foreach (var suitability in suitabilityList)
            {
                if (suitability.pawn != null && !assignedPawns.Contains(suitability.pawn))
                {
                    suitability.pawn.workSettings?.SetPriority(workType, DefaultPriority);
                }
            }
        }
    }
}