using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Defines the configuration settings for a single work type.
    /// Stores preferences for pawn count, priority, percentage mode, and advanced weighting options.
    /// </summary>
    public class WorkSettingValues : IExposable
    {
        /// <summary>
        /// Highest priority number RimWorld accepts (1 = most urgent, 4 = least).
        /// Kept as a single constant so band-scaling / future "More Work Priorities"
        /// support only needs to change one place instead of scattered literals.
        /// </summary>
        public const int MaxPriority = 4;

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

        /// <summary>
        /// Bottom (least urgent) priority of the backup band when
        /// <see cref="AutomatedWork_SaveData.gradualBackupScaling"/> is enabled.
        /// The band spans [<see cref="fallbackPriority"/> .. <see cref="backupBandBottom"/>];
        /// unselected pawns are spread across it by suitability rank instead of all
        /// receiving the same flat <see cref="fallbackPriority"/>.
        /// Ignored while gradual scaling is off, or when <see cref="fallbackPriority"/> is 0.
        /// </summary>
        public int backupBandBottom = MaxPriority;

        /// <summary>
        /// Bottom (least urgent) priority of the PRIMARY band when
        /// <see cref="AutomatedWork_SaveData.gradualBackupScaling"/> and
        /// <see cref="AutomatedWork_SaveData.bandPrimaryGroup"/> are both enabled.
        /// The band spans [<see cref="priority"/> .. <see cref="primaryBandBottom"/>];
        /// the selected (top-N) pawns are spread across it by suitability rank instead of
        /// all receiving the flat <see cref="priority"/>.
        /// Ignored while primary banding is off, or when it is not more urgent than <see cref="priority"/>.
        /// </summary>
        public int primaryBandBottom = MaxPriority;

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
            Scribe_Values.Look(ref backupBandBottom, "backupBandBottom", MaxPriority);
            Scribe_Values.Look(ref primaryBandBottom, "primaryBandBottom", MaxPriority);

            // Post-Load Validation
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (priority < 0) priority = 0;
                if (priority > MaxPriority) priority = MaxPriority;
                if (count < 0) count = 0;
                if (percentage < 0f) percentage = 0f;
                if (percentage > 1f) percentage = 1f;
                if (passionWeight < 0f) passionWeight = 0f;
                if (passionWeight > 3f) passionWeight = 3f;
                if (fallbackPriority < 0) fallbackPriority = 0;
                if (fallbackPriority > MaxPriority) fallbackPriority = MaxPriority;
                if (backupBandBottom < 0) backupBandBottom = 0;
                if (backupBandBottom > MaxPriority) backupBandBottom = MaxPriority;
                if (primaryBandBottom > MaxPriority) primaryBandBottom = MaxPriority;
                // A band bottom is never more urgent than its top.
                if (backupBandBottom < fallbackPriority) backupBandBottom = fallbackPriority;
                if (primaryBandBottom < priority) primaryBandBottom = priority;
            }
        }
    }
}