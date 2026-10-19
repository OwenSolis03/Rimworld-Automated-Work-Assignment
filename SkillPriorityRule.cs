using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Represents a single rule defining a work priority based on a specific skill level range.
    /// Instances of this class are used within the <see cref="ExpertModeRuleManager"/> to determine
    /// the appropriate priority for a pawn's work type based on their relevant skill.
    /// Implements <see cref="IExposable"/> to allow saving and loading with the game state
    /// using RimWorld's Scribe system.
    /// </summary>
    public class SkillPriorityRule : IExposable
    {
        private static int? _cachedMaxSkillLevel = null;
        
        /// <summary>
        /// Gets the maximum skill level cap for the current game session.
        /// Defaults to 20 (Vanilla), but attempts to dynamically detect higher limits
        /// if mods like "Endless Growth" or "Duck's Insane Skills" are active.
        /// Value is cached after the first successful detection.
        /// </summary>
        public static int MaxSkillLevel
        {
            get
            {
                if (_cachedMaxSkillLevel == null)
                {
                    _cachedMaxSkillLevel = 20; // Default vanilla cap
                    
                    try
                    {
                        // Detect the actual max skill level from currently spawned pawns
                        // This handles cases where mods raise the cap dynamically or via settings
                        var highestSkill = Find.Maps?
                            .SelectMany(m => m.mapPawns.AllPawnsSpawned)
                            .Where(p => p?.skills?.skills != null)
                            .SelectMany(p => p.skills.skills)
                            .Max(s => s?.Level ?? 0) ?? 20;
                        
                        // If we found skills above 20, assume extended skills are active
                        if (highestSkill > 20)
                        {
                            // Use detected max + buffer (10), capped at 50 to prevent extreme values/overflows
                            _cachedMaxSkillLevel = Math.Min(highestSkill + 10, 50);
                            Log.Message($"[AWA] Detected extended skills (highest: {highestSkill}). Using skill cap of {_cachedMaxSkillLevel}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[AWA] Could not detect max skill level: {ex.Message}. Using vanilla cap of 20.");
                    }
                }
                return _cachedMaxSkillLevel.Value;
            }
        }

        /// <summary>
        /// The minimum skill level (inclusive) required for this rule to apply.
        /// Range: 0 to <see cref="MaxSkillLevel"/>.
        /// </summary>
        public int MinSkill = 0;
        
        /// <summary>
        /// The maximum skill level (inclusive) required for this rule to apply.
        /// Range: 0 to <see cref="MaxSkillLevel"/>.
        /// </summary>
        public int MaxSkill = 20;
        
        /// <summary>
        /// The work priority level (1-4, where 1 is highest) to be assigned 
        /// if the pawn's skill falls within the [MinSkill, MaxSkill] range.
        /// </summary>
        public int Priority = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillPriorityRule"/> class with default values.
        /// Required for Scribe serialization.
        /// </summary>
        public SkillPriorityRule() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillPriorityRule"/> class with specific constraints.
        /// Automatically clamps values to valid ranges.
        /// </summary>
        /// <param name="min">Minimum skill level.</param>
        /// <param name="max">Maximum skill level.</param>
        /// <param name="prio">Priority level (1-4).</param>
        public SkillPriorityRule(int min, int max, int prio)
        {
            MinSkill = Mathf.Clamp(min, 0, MaxSkillLevel);
            MaxSkill = Mathf.Clamp(max, 0, MaxSkillLevel);
            Priority = Mathf.Clamp(prio, 1, 4);

            if (MinSkill > MaxSkill)
            {
                MinSkill = MaxSkill;
            }
        }

        /// <summary>
        /// Saves or loads the rule data.
        /// Includes post-load validation to ensure rules remain valid even if mods change.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref MinSkill, "minSkill", 0);
            Scribe_Values.Look(ref MaxSkill, "maxSkill", 20);
            Scribe_Values.Look(ref Priority, "priority", 3);

            // Post-load validation
            if (Scribe.mode == LoadSaveMode.LoadingVars || 
                Scribe.mode == LoadSaveMode.ResolvingCrossRefs || 
                Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Re-clamp to detected max (handles cases where a save is moved between mod lists)
                MinSkill = Mathf.Clamp(MinSkill, 0, MaxSkillLevel);
                MaxSkill = Mathf.Clamp(MaxSkill, 0, MaxSkillLevel);
                Priority = Mathf.Clamp(Priority, 1, 4);

                if (MinSkill > MaxSkill)
                {
                    MinSkill = MaxSkill;
                }
            }
        }
    }
}