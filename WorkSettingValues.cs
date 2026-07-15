using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Defines the configuration settings for a single work type.
    /// Stores preferences for pawn count, priority, percentage mode, and advanced weighting options.
    /// </summary>
    public class WorkSettingValues : IExposable
    {
        public int count = 3;
        public int priority = 3;
        public float percentage = 1f;
        public bool usePercentage = false;

        /// <summary>
        /// Determines the weight of passion in the suitability calculation.
        /// 0.0 = Ignore passion completely (skill only).
        /// 1.0 = Default balance.
        /// 2.0 = Double weight.
        /// 3.0 = Maximum emphasis on passion.
        /// </summary>
        public float passionWeight = 1f;

        /// <summary>
        /// Priority assigned to colonists NOT selected for the primary assignment.
        /// 0 = Disable work (default behavior).
        /// 1-4 = Set backup priority.
        /// Useful for tasks like Hauling/Cleaning where everyone should help if idle.
        /// </summary>
        public int fallbackPriority = 0;

        public WorkSettingValues() { }

        public WorkSettingValues(int count, int priority)
        {
            this.count = count;
            this.priority = priority;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref count, "count", 3);
            Scribe_Values.Look(ref priority, "priority", 3);
            Scribe_Values.Look(ref percentage, "percentage", 1f);
            Scribe_Values.Look(ref usePercentage, "usePercentage", false);
            Scribe_Values.Look(ref passionWeight, "passionWeight", 1f);
            Scribe_Values.Look(ref fallbackPriority, "fallbackPriority", 0);

            // Post-Load Validation
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (priority < 0) priority = 0;
                if (priority > 4) priority = 4;
                if (count < 0) count = 0;
                if (percentage < 0f) percentage = 0f;
                if (percentage > 1f) percentage = 1f;
                if (passionWeight < 0f) passionWeight = 0f;
                if (passionWeight > 3f) passionWeight = 3f;
                if (fallbackPriority < 0) fallbackPriority = 0;
                if (fallbackPriority > 4) fallbackPriority = 4;
            }
        }
    }
}