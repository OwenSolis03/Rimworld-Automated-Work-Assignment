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
    /// using RimWorld's Scribe system, particularly with <c>LookMode.Deep</c>.
    /// </summary>
    public class SkillPriorityRule : IExposable
    {
        /// <summary>
        /// Dynamically detected maximum skill level cap for the current game.
        /// Vanilla RimWorld = 20, but mods like Endless Growth can increase this.
        /// Cached after first detection to avoid repeated calculations.
        /// </summary>
        private static int? _cachedMaxSkillLevel = null;
        
        public static int MaxSkillLevel
        {
            get
            {
                if (_cachedMaxSkillLevel == null)
                {
                    _cachedMaxSkillLevel = 20; // Default vanilla cap
                    
                    try
                    {
                        // Detect the actual max skill level from spawned pawns
                        var highestSkill = Find.Maps?
                            .SelectMany(m => m.mapPawns.AllPawnsSpawned)
                            .Where(p => p?.skills?.skills != null)
                            .SelectMany(p => p.skills.skills)
                            .Max(s => s?.Level ?? 0) ?? 20;
                        
                        // If we found skills above 20, assume extended skills are active
                        if (highestSkill > 20)
                        {
                            // Use detected max + buffer, capped at 50 to prevent extreme values
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
        /// The minimum skill level (inclusive, 0-MaxSkillLevel) required for this rule to be considered applicable to a pawn.
        /// </summary>
        public int MinSkill = 0;
        
        /// <summary>
        /// The maximum skill level (inclusive, 0-MaxSkillLevel) allowed for this rule to be considered applicable to a pawn.
        /// </summary>
        public int MaxSkill = 20;
        
        /// <summary>
        /// The work priority level (1-4, where 1 is highest) to be assigned to the work type
        /// if a pawn's relevant skill level falls within the range defined by <see cref="MinSkill"/> and <see cref="MaxSkill"/>.
        /// Note: Priority 0 is generally reserved for disabling work.
        /// </summary>
        public int Priority = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillPriorityRule"/> class with default values.
        /// This parameterless constructor is required for the <see cref="IExposable"/> interface and
        /// for dynamic instantiation (e.g., when adding new rules in UI).
        /// </summary>
        public SkillPriorityRule() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillPriorityRule"/> class with specified values.
        /// Automatically clamps input values to valid ranges (Skill: 0-MaxSkillLevel, Priority: 1-4)
        /// and ensures MinSkill is not greater than MaxSkill.
        /// </summary>
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
        /// Handles the serialization (saving) and deserialization (loading) of the rule's data
        /// using RimWorld's Scribe system. Includes post-load validation to clamp values
        /// into valid ranges, ensuring data integrity even if the save file was manually edited
        /// or corrupted, or if skills have been extended by mods since the save was created.
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
                // Clamp to detected max (handles extended skills from mods)
                MinSkill = Mathf.Clamp(MinSkill, 0, MaxSkillLevel);
                MaxSkill = Mathf.Clamp(MaxSkill, 0, MaxSkillLevel);
                Priority = Mathf.Clamp(Priority, 1, 4);

                // Ensure consistency
                if (MinSkill > MaxSkill)
                {
                    Log.Warning($"[AWA Expert Mode] Loaded SkillPriorityRule had minSkill ({MinSkill}) > maxSkill ({MaxSkill}) for priority {Priority}. Clamping minSkill to maxSkill.");
                    MinSkill = MaxSkill;
                }
            }
        }
    }
}