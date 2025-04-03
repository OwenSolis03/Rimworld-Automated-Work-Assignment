using System.Collections.Generic;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Defines the settings for the Automated Work Assignment mod.
    /// Inherits from ModSettings to allow RimWorld to save/load the settings.
    /// </summary>
    public class AutomatedWorkSettings : ModSettings
    {
        /// <summary>
        /// Global toggle to enable or disable the mod's automatic assignment logic.
        /// Defaults to true.
        /// </summary>
        public bool modEnabled = true;

        /// <summary>
        /// Toggle to enable or disable the automatic daily refresh of assignments.
        /// Defaults to true.
        /// </summary>
        public bool enableDailyRefresh = true;

        /// <summary>
        /// Dictionary storing the specific settings (count and priority) for each WorkTypeDef.
        /// Key: defName of the WorkTypeDef (string).
        /// Value: WorkSettingValues object containing count and priority.
        /// Initialized on first use or after loading if null.
        /// </summary>
        public Dictionary<string, WorkSettingValues> workSettings = new Dictionary<string, WorkSettingValues>();

        /// <summary>
        /// List storing the ThingID (string) of pawns that should be excluded from automatic assignment.
        /// Initialized after loading if null.
        /// </summary>
        public List<string> excludedPawnIDs = new List<string>();

        /// <summary>
        /// List storing the defName (string) of WorkTypeDefs that should be excluded
        /// from the mod's automatic assignment logic entirely.
        /// Initialized after loading if null.
        /// </summary>
        public List<string> excludedWorkTypeDefNames = new List<string>(); // Field definition

        // --- Private working lists required by Scribe for dictionary serialization ---

        /// <summary>
        /// Temporary list used by Scribe_Collections.Look when saving/loading the keys of the workSettings dictionary.
        /// </summary>
        private List<string> workSettingsKeysWorkingList;

        /// <summary>
        /// Temporary list used by Scribe_Collections.Look when saving/loading the values of the workSettings dictionary.
        /// </summary>
        private List<WorkSettingValues> workSettingsValuesWorkingList;

        /// <summary>
        /// Retrieves the WorkSettingValues for a given WorkTypeDef defName.
        /// If settings for the specified work type do not exist, default settings are created, added to the dictionary, and returned.
        /// Ensures that the returned settings have valid priority (1-4) and count (>=0).
        /// </summary>
        /// <param name="workTypeDefName">The defName of the WorkTypeDef.</param>
        /// <returns>The WorkSettingValues instance for the specified work type.</returns>
        public WorkSettingValues GetWorkSetting(string workTypeDefName)
        {
            // Ensure the main dictionary is initialized
            if (workSettings == null)
            {
                Log.Warning("[AutoWork] workSettings dictionary was null in GetWorkSetting. Initializing.");
                workSettings = new Dictionary<string, WorkSettingValues>();
            }

            // Try to get existing settings
            if (!workSettings.TryGetValue(workTypeDefName, out WorkSettingValues setting))
            {
                // If not found, create default settings and add them
                setting = new WorkSettingValues();
                workSettings.Add(workTypeDefName, setting);
            }

            // Ensure data integrity
            if (setting.priority < 1) setting.priority = 1;
            if (setting.priority > 4) setting.priority = 4;
            if (setting.count < 0) setting.count = 0;

            return setting;
        }

        /// <summary>
        /// Handles saving and loading of all mod settings fields.
        /// Called by RimWorld during game save/load operations.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData(); // Call base method first

            // Save/Load simple boolean values
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref enableDailyRefresh, "enableDailyRefresh", true);

            // Save/Load the list of excluded pawn IDs
            Scribe_Collections.Look(ref excludedPawnIDs, "excludedPawnIDs", LookMode.Value);

            // Save/Load the list of excluded work type defNames
            Scribe_Collections.Look(ref excludedWorkTypeDefNames, "excludedWorkTypeDefNames", LookMode.Value); // Scribe call

            // Save/Load the dictionary of work settings
            Scribe_Collections.Look(ref workSettings, "workSettings", LookMode.Value, LookMode.Deep,
                ref workSettingsKeysWorkingList, ref workSettingsValuesWorkingList);

            // --- Post Load Initialization ---
            // After loading, ensure collections are not null
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workSettings == null)
                {
                    Log.Warning("[AutoWork] workSettings dictionary was null after loading. Initializing to empty.");
                    workSettings = new Dictionary<string, WorkSettingValues>();
                }
                if (excludedPawnIDs == null)
                {
                    Log.Warning("[AutoWork] excludedPawnIDs list was null after loading. Initializing to empty.");
                    excludedPawnIDs = new List<string>();
                }
                // Initialize the new list if it's null after loading
                if (excludedWorkTypeDefNames == null)
                {
                    Log.Warning("[AutoWork] excludedWorkTypeDefNames list was null after loading. Initializing to empty.");
                    excludedWorkTypeDefNames = new List<string>(); // Initialization
                }
            }
        }
    }
}