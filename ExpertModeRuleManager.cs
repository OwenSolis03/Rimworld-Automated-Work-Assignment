using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment 
{
    /// <summary>
    /// Manages the skill-based priority rules (Expert Mode) for a specific save game.
    /// This component is attached to the Game object and handles storing, loading,
    /// and applying the custom priority rules defined by the player for the Automated Work Assignment mod.
    /// </summary>
    public class ExpertModeRuleManager : GameComponent
    {
        /// <summary>
        /// The core data structure holding all defined Expert Mode rules. It maps each WorkTypeDef
        /// to a list of SkillPriorityRule objects that define priorities based on skill levels.
        /// This dictionary is serialized with the save game.
        /// </summary>
        public Dictionary<WorkTypeDef, List<SkillPriorityRule>> workTypeRules =
            new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();
        
        /// <summary>
        /// Stores the user's choice of assignment mode (fixed count or percentage) for each work type in Expert Mode.
        /// </summary>
        public Dictionary<WorkTypeDef, bool > usePercentage_EM = new Dictionary<WorkTypeDef, bool>();
        /// <summary>
        /// Stores the desired number of pawns to assign for each work type when using fixed count mode in Expert Mode.
        /// </summary>
        public Dictionary<WorkTypeDef, int > count_EM = new Dictionary<WorkTypeDef, int>();
        /// <summary>
        /// Stores the desired percentage of pawns to assign for each work type when using percentage mode in Expert Mode.
        /// </summary>
        public Dictionary<WorkTypeDef, float > percentage_EM = new Dictionary<WorkTypeDef, float>(); 

        /// <summary>
        /// A temporary list used by RimWorld's Scribe system to hold the dictionary keys (<c>WorkTypeDef</c>) during serialization.
        /// </summary>
        private List<WorkTypeDef> workTypeDefKeysWorkingList;
        
        /// <summary>
        /// A temporary list used by RimWorld's Scribe system to hold the dictionary values (<c>List&lt;SkillPriorityRule&gt;</c>) during serialization.
        /// </summary>
        private List<List<SkillPriorityRule>> skillPriorityRuleValuesWorkingList;

        /// <summary>
        /// Temporary lists used by the Scribe system to serialize the Expert Mode pawn limit settings.
        /// </summary>
        private List<WorkTypeDef> usePercentageKeys, countKeys, percentageKeys;
        private List<bool> usePercentageValues;
        private List<int> countValues;
        private List<float> percentageValues;

        /// <summary>
        /// A private helper struct to temporarily hold a pawn, their calculated suitability score, and passion for sorting.
        /// </summary>
        private struct PawnSuitability { public Pawn pawn; public float score; public Passion passion; }
        

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModeRuleManager"/> class.
        /// This constructor is required by RimWorld's GameComponent system.
        /// </summary>
        /// <param name="game">The current game instance this component belongs to.</param>
        public ExpertModeRuleManager(Game game) { }

        /// <summary>
        /// Applies all defined Expert Mode rules to eligible colonists. This is the core logic method for this system.
        /// </summary>
        /// <remarks>
        /// This method first selects the best pawns for a job up to a user-defined limit (either a fixed number or a percentage).
        /// It then applies the skill-based priority rules ONLY to that selected group of pawns.
        /// Pawns who are not selected will have their priority for that work type set to 0. It uses the global
        /// <see cref="WorkAssigner.CalculateSuitability"/> method to ensure consistent pawn ranking.
        /// </remarks>
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

                if (workDef == null || rules == null || !rules.Any() || (awaSaveData.excludedWorkTypeDefNames != null && awaSaveData.excludedWorkTypeDefNames.Contains(workDef.defName)))
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

                suitabilityList.Sort((a, b) =>
                {
                    int scoreComparison = b.score.CompareTo(a.score);
                    if (scoreComparison != 0) return scoreComparison;
                    return b.passion.CompareTo(a.passion);
                });

                bool usePercentage = usePercentage_EM.TryGetValue(workDef, out var val) ? val : false;
                int desiredCount;

                if (usePercentage)
                {
                    float percentage = percentage_EM.TryGetValue(workDef, out var p) ? p : 1f;
                    desiredCount = Mathf.RoundToInt(allColonists.Count * percentage);
                }
                else
                {
                    desiredCount = count_EM.TryGetValue(workDef, out var c) ? c : allColonists.Count;
                }
                desiredCount = Mathf.Clamp(desiredCount, 0, allColonists.Count);

                HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
                for (int i = 0; i < desiredCount; i++)
                {
                    Pawn pawnToAssign = suitabilityList[i].pawn;
                    SkillDef relevantSkill = workDef.relevantSkills?.FirstOrDefault();
                    int skillLevel = (relevantSkill != null) ? pawnToAssign.skills.GetSkill(relevantSkill)?.Level ?? 0 : 0;

                    SkillPriorityRule matchingRule = rules.FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);
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

        /// <summary>
        /// Retrieves a list of colonists who are eligible for Expert Mode rule application.
        /// </summary>
        /// <remarks>
        /// Filters for free, spawned colonists who are alive, not downed, player-controlled, not babies,
        /// and not on the main mod's pawn exclusion list.
        /// </remarks>
        /// <param name="saveData">The main mod's save data, containing the pawn exclusion list.</param>
        /// <returns>A list of eligible <see cref="Pawn"/> objects.</returns>
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

        /// <summary>
        /// Handles the serialization (saving and loading) of this component's data using RimWorld's Scribe system.
        /// </summary>
        /// <remarks>
        /// This method saves the <c>workTypeRules</c> dictionary and the Expert Mode pawn limit settings. 
        /// It also performs post-load cleanup to ensure data integrity.
        /// </remarks>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref workTypeRules, "workTypeRules_EM",
                LookMode.Def, LookMode.Deep,
                ref workTypeDefKeysWorkingList, ref skillPriorityRuleValuesWorkingList);
            
            Scribe_Collections.Look(ref usePercentage_EM, "usePercentage_EM", LookMode.Def, LookMode.Value, ref usePercentageKeys, ref usePercentageValues);
            Scribe_Collections.Look(ref count_EM, "count_EM", LookMode.Def, LookMode.Value, ref countKeys, ref countValues);
            Scribe_Collections.Look(ref percentage_EM, "percentage_EM", LookMode.Def, LookMode.Value, ref percentageKeys, ref percentageValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workTypeRules == null)
                {
                    workTypeRules = new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();
                }
                else
                {
                    workTypeRules = workTypeRules.Where(kvp => kvp.Key != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
                
                if (usePercentage_EM == null) usePercentage_EM = new Dictionary<WorkTypeDef, bool>();
                if (count_EM == null) count_EM = new Dictionary<WorkTypeDef, int>();
                if (percentage_EM == null) percentage_EM = new Dictionary<WorkTypeDef, float>();
            }
        }
    }
}