using Verse;

// Needed for IExposable and Scribe_Values

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Represents the specific settings (desired pawn count and target priority)
    /// for a single WorkTypeDef within the Automated Work Assignment mod.
    /// Implements IExposable to allow saving and loading via RimWorld's Scribe system.
    /// </summary>
    public class WorkSettingValues : IExposable
    {
        /// <summary>
        /// The desired number of pawns assigned to the associated WorkTypeDef.
        /// A count of 0 typically means the mod should ensure no pawns have this work type enabled (priority 0).
        /// Defaults to 0.
        /// </summary>
        public int count = 0;

        /// <summary>
        /// The target priority level (1-4, where 1 is highest) to assign to the selected pawns
        /// for the associated WorkTypeDef.
        /// Defaults to 3.
        /// </summary>
        public int priority = 3;

        /// <summary>
        /// Default constructor. Required for Scribe loading and general instantiation.
        /// Initializes with default values (count=0, priority=3).
        /// </summary>
        public WorkSettingValues() { }

        /// <summary>
        /// Parameterized constructor for creating an instance with specific values.
        /// </summary>
        /// <param name="count">Initial desired pawn count.</param>
        /// <param name="priority">Initial target priority.</param>
        public WorkSettingValues(int count, int priority)
        {
            this.count = count;
            this.priority = priority;
        }

        /// <summary>
        /// Handles saving and loading the 'count' and 'priority' fields.
        /// Called by the Scribe system (specifically when Scribe_Collections.Look uses LookMode.Deep).
        /// </summary>
        public void ExposeData()
        {
            // Save/Load the count value, defaulting to 0 if not found on load.
            Scribe_Values.Look(ref count, "count", 0);
            // Save/Load the priority value, defaulting to 3 if not found on load.
            Scribe_Values.Look(ref priority, "priority", 3);
        }
    }
}