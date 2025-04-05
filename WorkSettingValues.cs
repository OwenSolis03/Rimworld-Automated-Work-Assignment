using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Represents the specific settings for a single WorkTypeDef within the Automated Work Assignment mod.
    /// Now includes options for both fixed count and percentage-based assignment.
    /// Implements IExposable to allow saving and loading via RimWorld's Scribe system.
    /// </summary>
    public class WorkSettingValues : IExposable
    {
        /// <summary>
        /// The desired number of pawns assigned when using fixed count mode.
        /// Defaults to 0. Used when usePercentage is false.
        /// </summary>
        public int count = 0;

        /// <summary>
        /// The target priority level (1-4, where 1 is highest) to assign to the selected pawns.
        /// Defaults to 3.
        /// </summary>
        public int priority = 3;

        /// <summary>
        /// The desired percentage (0.0 to 1.0) of eligible pawns to assign when using percentage mode.
        /// Defaults to 0.1 (10%). Used when usePercentage is true.
        /// </summary>
        public float percentage = 0.1f;

        /// <summary>
        /// Determines whether to use the 'percentage' value (true) or the 'count' value (false).
        /// Defaults to false (use fixed count).
        /// </summary>
        public bool usePercentage = false;

        /// <summary>
        /// Default constructor. Required for Scribe loading and general instantiation.
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
        /// Handles saving and loading the setting fields for this work type.
        /// This method IMPLEMENTS the IExposable interface requirement.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref count, "count", 0);
            Scribe_Values.Look(ref priority, "priority", 3);
            Scribe_Values.Look(ref percentage, "percentage", 0.1f);
            Scribe_Values.Look(ref usePercentage, "usePercentage", false);

            // Post Load Clamping/Validation
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (priority < 1) priority = 1;
                if (priority > 4) priority = 4;
                if (count < 0) count = 0;
                if (percentage < 0f) percentage = 0f;
                if (percentage > 1f) percentage = 1f;
            }
        }
    }
}