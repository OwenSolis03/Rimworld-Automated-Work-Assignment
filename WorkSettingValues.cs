using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Defines the configuration settings for a single work type (e.g., 'Cooking', 'Mining')
    /// within the Automated Work Assignment mod. This class allows specifying how many pawns,
    /// or what percentage of eligible pawns, should be assigned to the work type and at what priority.
    /// It implements RimWorld's <see cref="IExposable"/> interface, enabling its data fields
    /// to be saved and loaded as part of the game's save file using the Scribe system.
    /// </summary>
    public class WorkSettingValues : IExposable
    {
        /// <summary>
        /// The target number of pawns to be assigned to this work type when <see cref="usePercentage"/> is false.
        /// For example, a value of 2 means the mod will try to assign the 2 most suitable pawns.
        /// Defaults to 3 if not otherwise specified or loaded. Cannot be negative.
        /// </summary>
        public int count = 3;

        /// <summary>
        /// The priority level (1 to 4, where 1 is the highest priority) that will be set for the
        /// pawns assigned to this work type by the mod.
        /// Defaults to 3 if not otherwise specified or loaded. Clamped between 1 and 4.
        /// </summary>
        public int priority = 3;

        /// <summary>
        /// The target percentage (as a fraction from 0.0 to 1.0) of *eligible* pawns to be assigned
        /// to this work type when <see cref="usePercentage"/> is true.
        /// For example, a value of 0.5 means the mod will try to assign the top 50% most suitable pawns.
        /// Defaults to 1.0 (100%) if not otherwise specified or loaded. Clamped between 0.0 and 1.0.
        /// </summary>
        public float percentage = 1f;

        /// <summary>
        /// A boolean flag determining the assignment mode for this work type.
        /// If true, the assignment logic uses the <see cref="percentage"/> value.
        /// If false, the assignment logic uses the fixed <see cref="count"/> value.
        /// Defaults to false (use fixed count mode).
        /// </summary>
        public bool usePercentage = false;

        /// <summary>
        /// Default constructor. Initializes a new instance of <see cref="WorkSettingValues"/> with default values.
        /// This constructor is required by RimWorld's Scribe system for loading saved data.
        /// </summary>
        public WorkSettingValues() { }

        /// <summary>
        /// Parameterized constructor. Initializes a new instance of <see cref="WorkSettingValues"/>
        /// with specified count and priority, using default values for other fields (percentage mode off).
        /// </summary>
        /// <param name="count">The initial desired number of pawns (fixed count mode).</param>
        /// <param name="priority">The initial target priority level (1-4).</param>
        public WorkSettingValues(int count, int priority)
        {
            this.count = count;
            this.priority = priority;
            // Defaults for percentage and usePercentage remain as defined in field initializers.
        }

        /// <summary>
        /// Implements the <see cref="IExposable.ExposeData"/> method required by RimWorld's save/load system.
        /// This method defines how the fields (<see cref="count"/>, <see cref="priority"/>,
        /// <see cref="percentage"/>, <see cref="usePercentage"/>) are written to or read from the save file
        /// using the <see cref="Scribe_Values"/> utility. It also includes validation logic
        /// (clamping) that runs after data is loaded to ensure values remain within valid ranges.
        /// </summary>
        public void ExposeData()
        {
            // Use Scribe_Values.Look to save/load each field with a unique label and a default value.
            Scribe_Values.Look(ref count, "count", 3); // "count" is the XML tag, 3 is the default if tag not found on load.
            Scribe_Values.Look(ref priority, "priority", 3);
            Scribe_Values.Look(ref percentage, "percentage", 1f);
            Scribe_Values.Look(ref usePercentage, "usePercentage", false);

            // Post-Load Validation/Clamping: Ensure loaded values are within expected bounds.
            // Scribe.mode tells us if we are currently saving, loading, or in another state.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Clamp priority between 1 and 4.
                if (priority < 1) priority = 1;
                if (priority > 4) priority = 4;
                // Ensure count is not negative.
                if (count < 0) count = 0;
                // Clamp percentage between 0.0 and 1.0.
                if (percentage < 0f) percentage = 0f;
                if (percentage > 1f) percentage = 1f;
            }
        }
    }
}