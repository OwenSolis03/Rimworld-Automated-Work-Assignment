using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment 
{
    /// <summary>
    /// A GameComponent responsible for storing and persisting the configuration rules for Expert Mode.
    /// It maintains a dictionary mapping WorkTypeDefs to their specific lists of SkillPriorityRules.
    /// This component focuses solely on data storage; the actual assignment logic is handled by WorkAssigner.
    /// </summary>
    public class ExpertModeRuleManager : GameComponent
    {
        /// <summary>
        /// Stores the list of priority rules for each work type.
        /// Key: The WorkTypeDef (e.g., Cooking).
        /// Value: A list of rules defining priority based on skill levels.
        /// </summary>
        public Dictionary<WorkTypeDef, List<SkillPriorityRule>> workTypeRules =
            new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();

        // working lists required for Scribe_Collections to save dictionaries
        private List<WorkTypeDef> workTypeDefKeysWorkingList;
        private List<List<SkillPriorityRule>> skillPriorityRuleValuesWorkingList;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpertModeRuleManager"/> class.
        /// </summary>
        /// <param name="game">The current game instance.</param>
        public ExpertModeRuleManager(Game game) { }

        /// <summary>
        /// Saves and loads the rule data using RimWorld's Scribe system.
        /// </summary>
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

            // Post-load initialization to ensure data integrity
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workTypeRules == null)
                {
                    workTypeRules = new Dictionary<WorkTypeDef, List<SkillPriorityRule>>();
                }
                else
                {
                    // Clean up potential null keys or values that might occur during mod updates/removals
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