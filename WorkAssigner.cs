using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Static utility class responsible for the core logic of assigning work priorities to pawns.
    /// It calculates suitability scores, processes exclusion lists, and applies priorities based on
    /// the selected assignment mode (Simple, Expert, or Hybrid).
    /// </summary>
    public static class WorkAssigner
    {
        private const int DefaultPriority = 0;

        /// <summary>
        /// Internal structure used to sort pawns based on their suitability score and passion.
        /// </summary>
        private struct PawnSuitability 
        { 
            public Pawn pawn; 
            public float score; 
            public Passion passion; 
        }
        
        /// <summary>
        /// Main entry point for the assignment logic.
        /// Iterates through all work types, filters eligible colonists, calculates required counts,
        /// and delegates the specific priority assignment to <see cref="AssignWorkPriorities"/>.
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
                Log.Error($"[AutoWork] Exception retrieving save data: {ex}");
                return;
            }

            if (saveData == null)
            {
                Log.ErrorOnce("[AutoWork] Per-save data is null in RefreshAssignments.", 1984775);
                return;
            }

            List<WorkTypeDef> workTypesToManage = null;
            try
            {
                workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                    .ToList();
            }
            catch (Exception ex) 
            { 
                Log.Error($"[AutoWork] Exception retrieving WorkTypeDefs: {ex}"); 
                return; 
            }

            if (Find.CurrentMap == null) return;

            foreach (WorkTypeDef workType in workTypesToManage)
            {
                try
                {
                    if (workType == null) continue;

                    // Skip work types specifically excluded by the user
                    if (saveData.excludedWorkTypeDefNames != null && saveData.excludedWorkTypeDefNames.Contains(workType.defName))
                    {
                        continue;
                    }

                    List<Pawn> eligibleForThisJob = GetEligibleColonistsForJob(saveData, workType);
                    if (!eligibleForThisJob.Any()) continue;

                    WorkSettingValues workSetting = saveData.GetWorkSetting(workType.defName);
                    if (workSetting == null) 
                    { 
                        Log.ErrorOnce($"[AutoWork] Null workSetting for {workType.defName}", workType.defName.GetHashCode() ^ 1); 
                        continue; 
                    }

                    // Determine how many pawns should be assigned
                    int finalDesiredCount;
                    if (workSetting.usePercentage)
                    {
                        finalDesiredCount = CalculateCountFromPercentage(workSetting.percentage, eligibleForThisJob.Count);
                    }
                    else
                    {
                        finalDesiredCount = workSetting.count;
                    }
                    finalDesiredCount = Mathf.Min(finalDesiredCount, eligibleForThisJob.Count);
                    
                    int targetPriority = workSetting.priority;
                    int fallbackPriority = workSetting.fallbackPriority;

                    if (finalDesiredCount > 0)
                    {
                        AssignWorkPriorities(workType, finalDesiredCount, targetPriority, fallbackPriority, eligibleForThisJob, saveData);
                    }
                    else
                    {
                        // If desired count is 0, disable this work for all eligible pawns
                        foreach (Pawn pawn in eligibleForThisJob)
                        {
                            pawn?.workSettings?.SetPriority(workType, DefaultPriority);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing '{workType?.defName ?? "NULL"}': {ex}");
                }
            }
        }

        /// <summary>
        /// Calculates the number of pawns to assign based on a target percentage.
        /// </summary>
        /// <param name="percentage">The target percentage (0.0 to 1.0).</param>
        /// <param name="totalEligibleCount">The total number of available colonists.</param>
        /// <returns>The integer count of pawns to assign.</returns>
        private static int CalculateCountFromPercentage(float percentage, int totalEligibleCount)
        {
            if (percentage <= 0f || totalEligibleCount <= 0) return 0;
            if (percentage >= 1f) return totalEligibleCount;
            float rawValue = percentage * totalEligibleCount;
            int calculatedCount = Mathf.RoundToInt(rawValue);
            
            // Ensure at least one pawn is assigned if percentage is positive but calculated count rounded to 0
            return (calculatedCount == 0 && percentage > 0f) ? 1 : Mathf.Min(calculatedCount, totalEligibleCount);
        }

        /// <summary>
        /// Retrieves a list of colonists eligible for general work assignment.
        /// Excludes pawns that are dead, downed, in a mental state, or globally excluded.
        /// </summary>
        /// <param name="saveData">The current save data containing exclusion lists.</param>
        /// <returns>A list of eligible pawns.</returns>
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
        /// Retrieves eligible colonists specifically for a target work type.
        /// Applies both global exclusions and per-job exclusions.
        /// </summary>
        /// <param name="saveData">The current save data.</param>
        /// <param name="workType">The work type being processed.</param>
        /// <returns>A filtered list of eligible pawns.</returns>
        public static List<Pawn> GetEligibleColonistsForJob(AutomatedWork_SaveData saveData, WorkTypeDef workType)
        {
            if (workType == null) return new List<Pawn>();
            
            List<string> globalExcludedIDs = saveData?.excludedPawnIDs ?? new List<string>();
            List<string> jobExcludedIDs = new List<string>();
            
            if (saveData?.perJobExcludedPawnIDs != null && 
                saveData.perJobExcludedPawnIDs.TryGetValue(workType.defName, out var jobExclusions))
            {
                jobExcludedIDs = jobExclusions ?? new List<string>();
            }
            
            if (Find.CurrentMap == null) return new List<Pawn>();

            try
            {
                return Find.CurrentMap.mapPawns.FreeColonists
                    .Where(p => p != null
                                && p.Spawned
                                && !p.Downed
                                && p.MentalStateDef == null
                                && p.workSettings != null
                                && !globalExcludedIDs.Contains(p.ThingID)
                                && !jobExcludedIDs.Contains(p.ThingID))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception in GetEligibleColonistsForJob: {ex}");
                return new List<Pawn>();
            }
        }

        /// <summary>
        /// Gets the total count of colonists eligible for general assignments.
        /// Used primarily for UI slider limits.
        /// </summary>
        internal static int GetEligibleColonistCount(AutomatedWork_SaveData saveData)
        {
            if (saveData == null) return 0;
            return GetEligibleColonists(saveData).Count;
        }

        /// <summary>
        /// Calculates a suitability score for a pawn performing a specific work type.
        /// Factors in skill level and passion. Supports vanilla passion values and 
        /// Vanilla Skills Expanded (VSE) custom passion rates via <see cref="ModDetector"/>.
        /// </summary>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <param name="workType">The work type to check.</param>
        /// <param name="saveData">Save data containing configuration (e.g., passion weight).</param>
        /// <returns>A float score, or -1f if the pawn is incapable.</returns>
        internal static float CalculateSuitability(Pawn pawn, WorkTypeDef workType, AutomatedWork_SaveData saveData)
        {
            try
            {
                if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) 
                    return -1f;

                WorkSettingValues workSetting = saveData?.GetWorkSetting(workType.defName);
                float passionWeight = workSetting?.passionWeight ?? 1f;

                SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                SkillRecord skill = relevantSkillDef != null ? pawn.skills.GetSkill(relevantSkillDef) : null;
                float score = skill?.Level ?? 1f;

                if (skill != null) 
                {
                    float passionBonus = 0f;
                    
                    // Attempt to use VSE (Vanilla Skills Expanded) logic if available
                    if (ModDetector.VSEIsActive && ModDetector.VSEReflectionSuccess) 
                    {
                        try 
                        {
                            if (ModDetector.VSE_PassionToDefDelegate != null && ModDetector.VSE_GetLearnRateDelegate != null)
                            {
                                object passionDefObj = ModDetector.VSE_PassionToDefDelegate(skill.passion);
                                if (passionDefObj != null) 
                                {
                                    float learnRateFactor = ModDetector.VSE_GetLearnRateDelegate(passionDefObj);
                                    passionBonus = Mathf.Max(0f, (learnRateFactor - 1.0f) * 10f);
                                }
                            }
                            // Fallback to slower reflection if delegates failed but VSE is present
                            else if (ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorField != null)
                            {
                                object passionDefObj = ModDetector.VSE_PassionToDefMethod.Invoke(null, new object[] { skill.passion });
                                if (passionDefObj != null && ModDetector.VSE_LearnRateFactorField.GetValue(passionDefObj) is float learnRateFactor)
                                {
                                    passionBonus = Mathf.Max(0f, (learnRateFactor - 1.0f) * 10f);
                                }
                            }
                        } 
                        catch (Exception ex) 
                        { 
                            Log.ErrorOnce($"[AWA] VSE error for {pawn.LabelShortCap},{skill.def.defName}: {ex.Message}", 
                                pawn.thingIDNumber ^ skill.def.shortHash ^ 2028); 
                        }
                    }
                    else
                    {
                        // Vanilla passion logic
                        passionBonus = skill.passion switch 
                        {
                            Passion.Major => 5f,
                            Passion.Minor => 2.5f,
                            _ => 0f
                        };
                    }
                    
                    score += passionBonus * passionWeight;
                }
                
                // Ensure a minimum score for existing skills
                if (score < 1f && relevantSkillDef != null) score = 1f;
                
                // Deterministic tie-breaker based on ThingID to prevent flickering assignments
                score += (pawn.thingIDNumber % 1000) * 0.00001f;
                
                return score;
            } 
            catch (Exception ex) 
            { 
                Log.Error($"[AutoWork] CalculateSuitability failed for {pawn?.ThingID},{workType?.defName}: {ex}"); 
                return -1f; 
            }
        }

        /// <summary>
        /// Applies priority settings to the list of eligible colonists.
        /// Sorts the colonists by suitability/passion and assigns priorities based on the selected mode.
        /// </summary>
        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, int fallbackPriority, List<Pawn> colonists, AutomatedWork_SaveData saveData)
        {
            if (workType == null || colonists == null || saveData == null) return;
            
            // 1. Calculate suitability for all candidates
            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
            foreach (Pawn pawn in colonists) 
            {
                if (pawn?.workSettings == null) continue;
                float score = CalculateSuitability(pawn, workType, saveData);
                if (score >= 0)
                {
                    Passion passion = Passion.None;
                    SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                    if (relevantSkillDef != null)
                    {
                        passion = pawn.skills.GetSkill(relevantSkillDef)?.passion ?? Passion.None;
                    }
                    suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score, passion = passion });
                }
                else 
                { 
                    // Pawn is ineligible (incapable), ensure priority is 0
                    pawn.workSettings.SetPriority(workType, DefaultPriority); 
                }
            }
            
            // 2. Sort candidates based on configuration
            if (saveData.prioritizePassionInExpertMode && saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Expert)
            {
                // Sort by Passion DESC, then Score DESC
                suitabilityList.Sort((a, b) =>
                {
                    int passionComparison = b.passion.CompareTo(a.passion);
                    if (passionComparison != 0) return passionComparison;
                    return b.score.CompareTo(a.score);
                });
            }
            else
            {
                // Default: Sort by Score DESC, then Passion DESC
                suitabilityList.Sort((a, b) =>
                {
                    int scoreComparison = b.score.CompareTo(a.score);
                    if (scoreComparison != 0) return scoreComparison;
                    return b.passion.CompareTo(a.passion);
                });
            }

            // 3. Prepare Expert Mode data if needed
            var expertManager = Current.Game.GetComponent<ExpertModeRuleManager>();
            bool expertRulesExist = expertManager?.workTypeRules.ContainsKey(workType) == true 
                                    && expertManager.workTypeRules[workType].Any();

            HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
            
            // 4. Assign priorities to top candidates
            for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++)
            {
                Pawn pawnToAssign = suitabilityList[i].pawn;
                int priorityToAssign = DefaultPriority;
                
                switch (saveData.assignmentMode)
                {
                    case AutomatedWork_SaveData.AssignmentMode.Simple:
                        priorityToAssign = targetPriority;
                        break;
                        
                    case AutomatedWork_SaveData.AssignmentMode.Expert:
                        if (expertRulesExist)
                        {
                            SkillDef relevantSkill = workType.relevantSkills?.FirstOrDefault();
                            int skillLevel;
                            
                            // Fallback to Social if work type has no skill (e.g., Cleaning/Hauling in some mods)
                            if (relevantSkill == null)
                            {
                                relevantSkill = SkillDefOf.Social;
                                skillLevel = pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0;
                                Log.WarningOnce(
                                    $"[AWA] {workType.defName} has no skill. Using Social as fallback.",
                                    workType.defName.GetHashCode() ^ 88734
                                );
                            }
                            else
                            {
                                skillLevel = pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0;
                            }
                            
                            // Find matching rule for skill level
                            SkillPriorityRule matchingRule = expertManager.workTypeRules[workType]
                                .FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);
                            
                            priorityToAssign = matchingRule?.Priority ?? DefaultPriority;
                        }
                        else
                        {
                            Log.WarningOnce($"[AWA] Expert Mode active but no rules for {workType.defName}. Using priority 0.", workType.defName.GetHashCode() ^ 77623);
                            priorityToAssign = DefaultPriority;
                        }
                        break;
                        
                    case AutomatedWork_SaveData.AssignmentMode.Hybrid:
                        priorityToAssign = targetPriority; // Default to Simple Mode target
                        
                        if (expertRulesExist)
                        {
                            SkillDef relevantSkill = workType.relevantSkills?.FirstOrDefault();
                            int skillLevel;
                            
                            if (relevantSkill == null)
                            {
                                relevantSkill = SkillDefOf.Social;
                                skillLevel = pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0;
                            }
                            else
                            {
                                skillLevel = pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0;
                            }
                            
                            SkillPriorityRule matchingRule = expertManager.workTypeRules[workType]
                                .FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);
                            
                            // If a rule matches, override the Simple priority
                            if (matchingRule != null)
                            {
                                priorityToAssign = matchingRule.Priority;
                            }
                        }
                        break;
                }
                
                // Emergency Priority Override (Doctor/Firefighter)
                if (saveData.forceEmergencyPriorities && 
                    (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter))
                {
                    priorityToAssign = 1;
                }
                
                pawnToAssign?.workSettings?.SetPriority(workType, priorityToAssign);
                if (pawnToAssign != null) assignedPawns.Add(pawnToAssign);
            }
            
            // 5. Apply Fallback Priority to unassigned pawns
            foreach (var suitability in suitabilityList)
            {
                if (suitability.pawn != null && !assignedPawns.Contains(suitability.pawn))
                {
                    // Fallback priority (usually 0, but user can set to 1-4 for things like Hauling)
                    suitability.pawn.workSettings?.SetPriority(workType, fallbackPriority);
                }
            }
        }
    }
}