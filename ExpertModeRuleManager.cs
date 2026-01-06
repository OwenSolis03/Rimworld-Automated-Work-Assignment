using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment 
{
    /// <summary>
    /// Manages skill-based priority rules (Expert Mode).
    /// SIMPLIFICADO: Ya no duplica count/percentage, usa los de AutomatedWork_SaveData.
    /// </summary>
    public class ExpertModeRuleManager : GameComponent
    {
        public Dictionary<WorkTypeDef, List<SkillPriorityRule>> workTypeRules =
            new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();

        private List<WorkTypeDef> workTypeDefKeysWorkingList;
        private List<List<SkillPriorityRule>> skillPriorityRuleValuesWorkingList;

        private struct PawnSuitability { public Pawn pawn; public float score; public Passion passion; }

        public ExpertModeRuleManager(Game game) { }

        /// <summary>
        /// Aplica las reglas de Expert Mode.
        /// SIMPLIFICADO: Usa count/percentage de Simple Mode en lugar de duplicarlos.
        /// </summary>
        public void AssignPrioritiesBasedOnRules()
        {
            if (workTypeRules == null || !workTypeRules.Any(kvp => kvp.Value.Any()))
            {
                return;
            }

            AutomatedWork_SaveData awaSaveData = Current.Game?.GetComponent<AutomatedWork_SaveData>();
            if (awaSaveData == null)
            {
                Log.ErrorOnce("[AWA] Expert Mode could not find base AWA save data.", 9487201);
                return;
            }

            List<Pawn> allColonists = GetEligibleColonistsForExpertMode(awaSaveData);
            if (allColonists == null || !allColonists.Any())
            {
                return;
            }

            foreach (var kvp in this.workTypeRules)
            {
                WorkTypeDef workDef = kvp.Key;
                List<SkillPriorityRule> rules = kvp.Value;

                if (workDef == null || rules == null || !rules.Any() || 
                    (awaSaveData.excludedWorkTypeDefNames != null && awaSaveData.excludedWorkTypeDefNames.Contains(workDef.defName)))
                {
                    continue;
                }
                
                List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
                foreach (Pawn pawn in allColonists)
                {
                    if (pawn.WorkTypeIsDisabled(workDef))
                    {
                        pawn.workSettings.SetPriority(workDef, 0);
                        continue;
                    }

                    float score = WorkAssigner.CalculateSuitability(pawn, workDef);
                    Passion passion = Passion.None;
                    SkillDef relevantSkillDef = workDef.relevantSkills?.FirstOrDefault();
                    if (relevantSkillDef != null)
                    {
                        passion = pawn.skills.GetSkill(relevantSkillDef)?.passion ?? Passion.None;
                    }
                    suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score, passion = passion });
                }

                // NUEVA FEATURE: Respetar prioritizePassionInExpertMode
                if (awaSaveData.prioritizePassionInExpertMode)
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

                // SIMPLIFICADO: Usar settings de Simple Mode
                WorkSettingValues workSetting = awaSaveData.GetWorkSetting(workDef.defName);
                int desiredCount;

                if (workSetting.usePercentage)
                {
                    desiredCount = Mathf.RoundToInt(allColonists.Count * workSetting.percentage);
                }
                else
                {
                    desiredCount = workSetting.count;
                }
                desiredCount = Mathf.Clamp(desiredCount, 0, allColonists.Count);

                HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
                for (int i = 0; i < desiredCount && i < suitabilityList.Count; i++)
                {
                    Pawn pawnToAssign = suitabilityList[i].pawn;
                    SkillDef relevantSkill = workDef.relevantSkills?.FirstOrDefault();
                    int skillLevel;
                    
                    // FIX: Manejar trabajos sin skill
                    if (relevantSkill == null)
                    {
                        relevantSkill = SkillDefOf.Social;
                        skillLevel = pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0;
                    }
                    else
                    {
                        skillLevel = pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0;
                    }

                    SkillPriorityRule matchingRule = rules
                        .FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);
                    int priorityToSet = matchingRule?.Priority ?? 0;

                    if (pawnToAssign.workSettings.GetPriority(workDef) != priorityToSet)
                    {
                        pawnToAssign.workSettings.SetPriority(workDef, priorityToSet);
                    }
                    assignedPawns.Add(pawnToAssign);
                }

                foreach (var suitability in suitabilityList)
                {
                    if (!assignedPawns.Contains(suitability.pawn))
                    {
                        suitability.pawn.workSettings.SetPriority(workDef, 0);
                    }
                }
            }
        }

        private List<Pawn> GetEligibleColonistsForExpertMode(AutomatedWork_SaveData saveData)
        {
            List<string> excludedIDs = saveData?.excludedPawnIDs ?? new List<string>();
            if (Find.CurrentMap?.mapPawns == null) return new List<Pawn>();

            try
            {
                return Find.CurrentMap.mapPawns.FreeColonistsSpawned
                           ?.Where(p => p != null
                                        && !p.Dead
                                        && !p.Downed
                                        && p.Faction == Faction.OfPlayer
                                        && p.HostFaction == null
                                        && p.workSettings != null
                                        && !p.DevelopmentalStage.Baby()
                                        && !excludedIDs.Contains(p.ThingID)
                           )
                           .ToList()
                       ?? new List<Pawn>();
            }
            catch (Exception ex)
            {
                Log.Error($"[AWA] Exception in GetEligibleColonistsForExpertMode: {ex}");
                return new List<Pawn>();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Collections.Look(
                ref workTypeRules, 
                "workTypeRules_EM",
                LookMode.Def, 
                LookMode.Deep,
                ref workTypeDefKeysWorkingList, 
                ref skillPriorityRuleValuesWorkingList
            );

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workTypeRules == null)
                {
                    workTypeRules = new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();
                }
                else
                {
                    workTypeRules = workTypeRules
                        .Where(kvp => kvp.Key != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    
                    foreach (var key in workTypeRules.Keys.ToList())
                    {
                        if (workTypeRules[key] == null)
                        {
                            workTypeRules[key] = new List<SkillPriorityRule>();
                        }
                        else
                        {
                            workTypeRules[key].RemoveAll(rule => rule == null);
                        }
                    }
                }
            }
        }
    }
}