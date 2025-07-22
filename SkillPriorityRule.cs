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
        /// The minimum skill level (inclusive, 0-20) required for this rule to be considered applicable to a pawn.
        /// </summary>
        public int MinSkill = 0;
        /// <summary>
        /// The maximum skill level (inclusive, 0-20) allowed for this rule to be considered applicable to a pawn.
        /// </summary>
        public int MaxSkill = 20;
        /// <summary>
        /// The work priority level (1-4, where 1 is highest) to be assigned to the work type
        /// if a pawn's relevant skill level falls within the range defined by <see cref="MinSkill"/> and <see cref="MaxSkill"/>.
        /// Note: Priority 0 is generally reserved for disabling work.
        /// </summary>
        public int Priority = 3; // Defaulting to priority 3

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillPriorityRule"/> class with default values.
        /// This parameterless constructor is required for the <see cref="IExposable"/> interface and
        /// for dynamic instantiation (e.g., when adding new rules in UI).
        /// Defaults: MinSkill=0, MaxSkill=20, Priority=3.
        /// </summary>
        public SkillPriorityRule() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkillPriorityRule"/> class with specified values.
        /// Provides a convenient way to create and initialize a rule object.
        /// Automatically clamps input values to valid ranges (Skill: 0-20, Priority: 1-4)
        /// and ensures MinSkill is not greater than MaxSkill.
        /// </summary>
        /// <param name="min">The desired minimum skill level (will be clamped between 0 and 20).</param>
        /// <param name="max">The desired maximum skill level (will be clamped between 0 and 20).</param>
        /// <param name="prio">The desired work priority (will be clamped between 1 and 4).</param>
        public SkillPriorityRule(int min, int max, int prio)
        {
            // Clamp inputs to valid ranges using Mathf.Clamp.
            MinSkill = Mathf.Clamp(min, 0, 20);
            MaxSkill = Mathf.Clamp(max, 0, 20);
            Priority = Mathf.Clamp(prio, 1, 4); // Clamp priority between 1 and 4 (standard RimWorld active priorities)

            // Ensure MinSkill does not exceed MaxSkill after potential clamping.
            if (MinSkill > MaxSkill)
            {
                // If clamping resulted in min > max, set min equal to max.
                MinSkill = MaxSkill;
            }
        }

        /// <summary>
        /// Handles the serialization (saving) and deserialization (loading) of the rule's data
        /// using RimWorld's Scribe system. This method is called automatically by the game
        /// when saving or loading the parent structure (like the list within <see cref="ExpertModeRuleManager"/>)
        /// that uses <c>LookMode.Deep</c>. Includes post-load validation to clamp values
        /// into valid ranges, ensuring data integrity even if the save file was manually edited
        /// or corrupted.
        /// </summary>
        public void ExposeData()
        {
            // Save/Load the individual fields with default values provided.
            Scribe_Values.Look(ref MinSkill, "minSkill", 0);
            Scribe_Values.Look(ref MaxSkill, "maxSkill", 20);
            Scribe_Values.Look(ref Priority, "priority", 3);

            // Perform validation *after* loading all values.
            // This ensures that loaded values (potentially from older versions or corrupted saves)
            // are brought back into valid ranges.
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Re-apply clamping to ensure values are within expected bounds after loading.
                MinSkill = Mathf.Clamp(MinSkill, 0, 20);
                MaxSkill = Mathf.Clamp(MaxSkill, 0, 20);
                Priority = Mathf.Clamp(Priority, 1, 4); // Clamp to 1-4 for active priorities

                // Ensure MinSkill <= MaxSkill consistency after potentially loading invalid data.
                if (MinSkill > MaxSkill)
                {
                    Log.Warning($"[AWA Expert Mode] Loaded SkillPriorityRule had minSkill ({MinSkill}) > maxSkill ({MaxSkill}) for priority {Priority}. Clamping minSkill ({MinSkill}) down to maxSkill ({MaxSkill}).");
                    MinSkill = MaxSkill; // Correct the invalid state by setting min equal to max.
                }
            }
        }
    }
}