using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Core logic for calculating pawn suitability and assigning work priorities.
    /// UPDATED: Soporte completo para AssignmentMode, per-job exclusions, passion weight, fallback priority.
    /// </summary>
    public static class WorkAssigner
    {
        private const int DefaultPriority = 0;
        private struct PawnSuitability { public Pawn pawn; public float score; public Passion passion; }
        
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

                    if (saveData.excludedWorkTypeDefNames != null && saveData.excludedWorkTypeDefNames.Contains(workType.defName))
                    {
                        continue;
                    }

                    // NUEVA FEATURE: Usar GetEligibleColonistsForJob que respeta exclusiones por trabajo
                    List<Pawn> eligibleForThisJob = GetEligibleColonistsForJob(saveData, workType);
                    if (!eligibleForThisJob.Any()) continue;

                    WorkSettingValues workSetting = saveData.GetWorkSetting(workType.defName);
                    if (workSetting == null) 
                    { 
                        Log.ErrorOnce($"[AutoWork] Null workSetting for {workType.defName}", workType.defName.GetHashCode() ^ 1); 
                        continue; 
                    }

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
                    int fallbackPriority = workSetting.fallbackPriority; // NUEVA FEATURE

                    if (finalDesiredCount > 0)
                    {
                        AssignWorkPriorities(workType, finalDesiredCount, targetPriority, fallbackPriority, eligibleForThisJob, saveData);
                    }
                    else
                    {
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

        private static int CalculateCountFromPercentage(float percentage, int totalEligibleCount)
        {
            if (percentage <= 0f || totalEligibleCount <= 0) return 0;
            if (percentage >= 1f) return totalEligibleCount;
            float rawValue = percentage * totalEligibleCount;
            int calculatedCount = Mathf.RoundToInt(rawValue);
            
            return (calculatedCount == 0 && percentage > 0f) ? 1 : Mathf.Min(calculatedCount, totalEligibleCount);
        }

        /// <summary>
        /// Eligibilidad GLOBAL (no considera exclusiones por trabajo)
        /// </summary>
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
        /// NUEVA FEATURE: Eligibilidad para un TRABAJO ESPECÍFICO (respeta exclusiones por trabajo)
        /// </summary>
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

        internal static int GetEligibleColonistCount(AutomatedWork_SaveData saveData)
        {
            if (saveData == null) return 0;
            return GetEligibleColonists(saveData).Count;
        }

        /// <summary>
        /// Calcula suitability con NUEVA FEATURE: passionWeight configurable
        /// FIX CRÍTICO: Usa delegados rápidos en lugar de Invoke (10-50x más rápido)
        /// </summary>
        internal static float CalculateSuitability(Pawn pawn, WorkTypeDef workType)
        {
            try
            {
                if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) 
                    return -1f;

                var saveData = AutomatedWorkAssignmentMod.CurrentData;
                WorkSettingValues workSetting = saveData?.GetWorkSetting(workType.defName);
                float passionWeight = workSetting?.passionWeight ?? 1f; // NUEVA FEATURE

                SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                SkillRecord skill = relevantSkillDef != null ? pawn.skills.GetSkill(relevantSkillDef) : null;
                float score = skill?.Level ?? 1f;

                if (skill != null) 
                {
                    float passionBonus = 0f;
                    
                    // FIX CRÍTICO: Usar delegados rápidos
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
                            else if (ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorField != null)
                            {
                                // Fallback a Invoke si delegados fallaron
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
                        // Vanilla passion (aditivo para que funcione con passionWeight)
                        passionBonus = skill.passion switch 
                        {
                            Passion.Major => 5f,
                            Passion.Minor => 2.5f,
                            _ => 0f
                        };
                    }
                    
                    // NUEVA FEATURE: Aplicar peso de pasión
                    score += passionBonus * passionWeight;
                }
                
                if (score < 1f && relevantSkillDef != null) score = 1f;
                
                // FIX: Desempate determinista
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
        /// UPDATED: Ahora soporta AssignmentMode (Simple/Expert/Hybrid), fallbackPriority, passion priority
        /// </summary>
        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, int fallbackPriority, List<Pawn> colonists, AutomatedWork_SaveData saveData)
        {
            if (workType == null || colonists == null || saveData == null) return;
            
            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
            foreach (Pawn pawn in colonists) 
            {
                if (pawn?.workSettings == null) continue;
                float score = CalculateSuitability(pawn, workType);
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
                    pawn.workSettings.SetPriority(workType, DefaultPriority); 
                }
            }
            
            // NUEVA FEATURE: Ordenamiento por pasión primero si está activado
            if (saveData.prioritizePassionInExpertMode && saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Expert)
            {
                suitabilityList.Sort((a, b) =>
                {
                    int passionComparison = b.passion.CompareTo(a.passion);
                    if (passionComparison != 0) return passionComparison;
                    return b.score.CompareTo(a.score);
                });
            }
            else
            {
                suitabilityList.Sort((a, b) =>
                {
                    int scoreComparison = b.score.CompareTo(a.score);
                    if (scoreComparison != 0) return scoreComparison;
                    return b.passion.CompareTo(a.passion);
                });
            }

            var expertManager = Current.Game.GetComponent<ExpertModeRuleManager>();
            bool expertRulesExist = expertManager?.workTypeRules.ContainsKey(workType) == true 
                                    && expertManager.workTypeRules[workType].Any();

            HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
            
            // NUEVA FEATURE: Lógica de AssignmentMode
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
                            
                            // FIX: Manejar trabajos sin skill
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
                        priorityToAssign = targetPriority;
                        
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
                            
                            if (matchingRule != null)
                            {
                                priorityToAssign = matchingRule.Priority;
                            }
                        }
                        break;
                }
                
                // NUEVA FEATURE: Emergency override configurable
                if (saveData.forceEmergencyPriorities && 
                    (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter))
                {
                    priorityToAssign = 1;
                }
                
                pawnToAssign?.workSettings?.SetPriority(workType, priorityToAssign);
                if (pawnToAssign != null) assignedPawns.Add(pawnToAssign);
            }
            
            // NUEVA FEATURE: Aplicar fallbackPriority a pawns no seleccionados
            foreach (var suitability in suitabilityList)
            {
                if (suitability.pawn != null && !assignedPawns.Contains(suitability.pawn))
                {
                    suitability.pawn.workSettings?.SetPriority(workType, fallbackPriority);
                }
            }
        }
    }
}