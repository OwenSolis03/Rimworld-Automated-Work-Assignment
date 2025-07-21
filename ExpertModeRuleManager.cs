using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
        /// A temporary list used by RimWorld's Scribe system to hold the dictionary keys (<c>WorkTypeDef</c>) during serialization.
        /// </summary>
        private List<WorkTypeDef> workTypeDefKeysWorkingList;
        
        /// <summary>
        /// A temporary list used by RimWorld's Scribe system to hold the dictionary values (<c>List&lt;SkillPriorityRule&gt;</c>) during serialization.
        /// </summary>
        private List<List<SkillPriorityRule>> skillPriorityRuleValuesWorkingList;

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
        /// This method is called by the main <c>WorkAssigner</c> after its simple logic has run. It iterates through
        /// all pawns and all work types that have expert rules defined. For each, it checks:
        /// 1. If the work type is excluded in the main mod settings.
        /// 2. If the pawn has the work type disabled in the vanilla work tab.
        /// 3. It finds a matching rule based on the pawn's skill level.
        /// If a rule matches, it applies the priority from that rule. If no rule matches, it sets the priority to 0 (disabled).
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

            List<Pawn> colonists = GetEligibleColonistsForExpertMode(awaSaveData);
            if (colonists == null || !colonists.Any())
            {
                return;
            }

            foreach (Pawn pawn in colonists)
            {
                if (pawn?.skills == null || pawn.workSettings == null || pawn.Dead || pawn.Downed) continue;

                foreach (var kvp in this.workTypeRules)
                {
                    WorkTypeDef workDef = kvp.Key;
                    List<SkillPriorityRule> rules = kvp.Value;
                    
                    if (awaSaveData.excludedWorkTypeDefNames != null && awaSaveData.excludedWorkTypeDefNames.Contains(workDef.defName))
                    {
                        continue;
                    }

                    if (workDef == null || rules == null || !rules.Any()) continue;

                    int priorityToSet = 0; // Default to disabled

                    if (pawn.WorkTypeIsDisabled(workDef))
                    {
                        priorityToSet = 0;
                    }
                    else
                    {
                        SkillDef relevantSkill = workDef.relevantSkills?.FirstOrDefault();
                        int skillLevel = (relevantSkill != null) ? pawn.skills.GetSkill(relevantSkill)?.Level ?? 0 : 0;

                        SkillPriorityRule matchingRule = rules.FirstOrDefault(rule => skillLevel >= rule.MinSkill && skillLevel <= rule.MaxSkill);

                        priorityToSet = matchingRule?.Priority ?? 0;
                    }

                    if (pawn.workSettings.GetPriority(workDef) != priorityToSet)
                    {
                        pawn.workSettings.SetPriority(workDef, priorityToSet);
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
        /// This method saves the <c>workTypeRules</c> dictionary. It also performs post-load cleanup to ensure data integrity,
        /// such as removing rules for <c>WorkTypeDef</c>s from uninstalled mods and clearing any null entries from rule lists.
        /// </remarks>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref workTypeRules, "workTypeRules_EM",
                LookMode.Def, LookMode.Deep,
                ref workTypeDefKeysWorkingList, ref skillPriorityRuleValuesWorkingList);

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
            }
        }
    }
}