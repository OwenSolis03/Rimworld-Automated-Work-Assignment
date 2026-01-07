using System.Collections.Generic;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// A RimWorld <see cref="GameComponent"/> dedicated to storing and managing all mod-related settings
    /// that are specific to a particular save game. This component persists configuration data
    /// such as assignment modes, exclusions, and per-job settings within the save file.
    /// </summary>
    public class AutomatedWork_SaveData : GameComponent
    {
        // --- Core Settings ---
        
        public bool modEnabled = true;
        public bool enableDailyRefresh = true;

        /// <summary>
        /// Defines the active assignment mode for the automated work system.
        /// </summary>
        public enum AssignmentMode
        {
            /// <summary>
            /// Uses only the simple sliders (Count/Percentage, Priority) for assignments.
            /// </summary>
            Simple,
            /// <summary>
            /// Uses only the detailed skill-based rules defined in Expert Mode.
            /// </summary>
            Expert,
            /// <summary>
            /// Tries to apply Expert Mode rules first; if no rule matches, falls back to Simple Mode settings.
            /// </summary>
            Hybrid
        }
        
        /// <summary>
        /// The currently active mode determining how priorities are calculated.
        /// </summary>
        public AssignmentMode assignmentMode = AssignmentMode.Simple;

        /// <summary>
        /// If true, critical emergency jobs (Doctor, Firefighter) are forced to Priority 1,
        /// overriding other user configurations to ensure colony safety.
        /// </summary>
        public bool forceEmergencyPriorities = true;

        /// <summary>
        /// If true, Expert Mode sorting logic prioritizes Passion over Skill level.
        /// Useful for prioritizing the training of passionate pawns even if their current skill is lower.
        /// </summary>
        public bool prioritizePassionInExpertMode = false;

        // --- Work Type Settings ---

        public Dictionary<string, WorkSettingValues> workSettings = new Dictionary<string, WorkSettingValues>();

        // --- Exclusion Lists ---

        /// <summary>
        /// Global list of Pawn ThingIDs that are completely excluded from the automated assignment system.
        /// </summary>
        public List<string> excludedPawnIDs = new List<string>();

        /// <summary>
        /// List of WorkTypeDef names (e.g., "Cooking", "Cleaning") that are excluded from automation.
        /// </summary>
        public List<string> excludedWorkTypeDefNames = new List<string>();

        /// <summary>
        /// Dictionary storing specific pawn exclusions per job type.
        /// Key: WorkTypeDef name. Value: List of excluded Pawn ThingIDs.
        /// </summary>
        public Dictionary<string, List<string>> perJobExcludedPawnIDs = new Dictionary<string, List<string>>();

        // --- Private working lists for Scribe (Serialization) ---
        
        private List<string> workSettingsKeysWorkingList;
        private List<WorkSettingValues> workSettingsValuesWorkingList;
        private List<string> perJobExclusionKeysWorkingList;
        private List<List<string>> perJobExclusionValuesWorkingList;

        public AutomatedWork_SaveData(Game game) { }

        /// <summary>
        /// Retrieves the settings for a specific work type.
        /// If no settings exist for the specified type, a new default instance is created and stored.
        /// </summary>
        /// <param name="workTypeDefName">The defName of the WorkTypeDef to look up.</param>
        /// <returns>The <see cref="WorkSettingValues"/> object for the specified work type.</returns>
        public WorkSettingValues GetWorkSetting(string workTypeDefName)
        {
            if (workSettings == null)
            {
                Log.Warning("[AutoWork] SaveData: workSettings was null in GetWorkSetting. Initializing.");
                workSettings = new Dictionary<string, WorkSettingValues>();
            }

            if (!workSettings.TryGetValue(workTypeDefName, out WorkSettingValues setting))
            {
                setting = new WorkSettingValues();
                workSettings.Add(workTypeDefName, setting);
            }

            // Range validation
            if (setting.priority < 1) setting.priority = 1;
            if (setting.priority > 4) setting.priority = 4;
            if (setting.count < 0) setting.count = 0;
            if (setting.percentage < 0f) setting.percentage = 0f;
            if (setting.percentage > 1f) setting.percentage = 1f;

            return setting;
        }

        /// <summary>
        /// Checks if a specific pawn is excluded from a specific job type.
        /// </summary>
        /// <param name="pawnID">The unique ThingID of the pawn.</param>
        /// <param name="workTypeDefName">The defName of the work type.</param>
        /// <returns>True if the pawn is excluded from this specific job; otherwise, false.</returns>
        public bool IsPawnExcludedFromJob(string pawnID, string workTypeDefName)
        {
            if (perJobExcludedPawnIDs == null) return false;
            if (!perJobExcludedPawnIDs.TryGetValue(workTypeDefName, out var excludedList)) return false;
            return excludedList?.Contains(pawnID) ?? false;
        }

        /// <summary>
        /// Saves and loads the component's data using RimWorld's Scribe system.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            // Basic Settings
            Scribe_Values.Look(ref modEnabled, "modEnabled_perSave", true);
            Scribe_Values.Look(ref enableDailyRefresh, "enableDailyRefresh_perSave", true);
            
            // Advanced Features
            Scribe_Values.Look(ref assignmentMode, "assignmentMode", AssignmentMode.Simple);
            Scribe_Values.Look(ref forceEmergencyPriorities, "forceEmergencyPriorities", true);
            Scribe_Values.Look(ref prioritizePassionInExpertMode, "prioritizePassionInExpertMode", false);

            // Global Exclusion Lists
            Scribe_Collections.Look(ref excludedPawnIDs, "excludedPawnIDs_perSave", LookMode.Value);
            Scribe_Collections.Look(ref excludedWorkTypeDefNames, "excludedWorkTypeDefNames_perSave", LookMode.Value);

            // Work Settings
            Scribe_Collections.Look(ref workSettings, "workSettings_perSave", LookMode.Value, LookMode.Deep,
                ref workSettingsKeysWorkingList, ref workSettingsValuesWorkingList);

            // Job-Specific Exclusions
            Scribe_Collections.Look(ref perJobExcludedPawnIDs, "perJobExcludedPawnIDs", 
                LookMode.Value, LookMode.Deep,
                ref perJobExclusionKeysWorkingList, ref perJobExclusionValuesWorkingList);

            // Post-load initialization and null checks
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workSettings == null) workSettings = new Dictionary<string, WorkSettingValues>();
                if (excludedPawnIDs == null) excludedPawnIDs = new List<string>();
                if (excludedWorkTypeDefNames == null) excludedWorkTypeDefNames = new List<string>();
                if (perJobExcludedPawnIDs == null) perJobExcludedPawnIDs = new Dictionary<string, List<string>>();
            }
        }
    }
}