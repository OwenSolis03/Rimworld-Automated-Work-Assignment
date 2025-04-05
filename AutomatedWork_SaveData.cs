using System.Collections.Generic;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// GameComponent responsible for storing and managing the mod's settings on a per-save basis.
    /// Settings are loaded and saved with the game save file.
    /// </summary>
    public class AutomatedWork_SaveData : GameComponent
    {
        // --- Settings Fields ---

        /// <summary> Global toggle for the mod's logic within this specific save game. </summary>
        public bool modEnabled = true;
        /// <summary> Toggle for automatic daily refresh within this specific save game. </summary>
        public bool enableDailyRefresh = true;
        /// <summary> Per-work type settings (count, priority, mode) for this save game. </summary>
        public Dictionary<string, WorkSettingValues> workSettings = new Dictionary<string, WorkSettingValues>();
        /// <summary> List of pawn ThingIDs excluded from assignment in this save game. </summary>
        public List<string> excludedPawnIDs = new List<string>();
        /// <summary> List of WorkTypeDef defNames excluded from assignment in this save game. </summary>
        public List<string> excludedWorkTypeDefNames = new List<string>();

        // --- Private working lists required by Scribe for dictionary serialization ---
        /// <summary> Temporary list used by Scribe_Collections.Look when saving/loading the keys of the workSettings dictionary. </summary>
        private List<string> workSettingsKeysWorkingList;
        /// <summary> Temporary list used by Scribe_Collections.Look when saving/loading the values of the workSettings dictionary. </summary>
        private List<WorkSettingValues> workSettingsValuesWorkingList;

        /// <summary>
        /// Required constructor for GameComponents.
        /// </summary>
        /// <param name="game">The current game instance.</param>
        public AutomatedWork_SaveData(Game game) { }

        /// <summary>
        /// Retrieves the WorkSettingValues for a given WorkTypeDef defName for this save game.
        /// Creates default settings if they don't exist. Ensures values are valid.
        /// </summary>
        /// <param name="workTypeDefName">The defName of the WorkTypeDef.</param>
        /// <returns>The WorkSettingValues instance.</returns>
        public WorkSettingValues GetWorkSetting(string workTypeDefName)
        {
            if (workSettings == null)
            {
                Log.Warning("[AutoWork] SaveData: workSettings dictionary was null in GetWorkSetting. Initializing.");
                workSettings = new Dictionary<string, WorkSettingValues>();
            }

            if (!workSettings.TryGetValue(workTypeDefName, out WorkSettingValues setting))
            {
                setting = new WorkSettingValues();
                workSettings.Add(workTypeDefName, setting);
            }

            // Clamping values after retrieving/creating
            if (setting.priority < 1) setting.priority = 1;
            if (setting.priority > 4) setting.priority = 4;
            if (setting.count < 0) setting.count = 0;
            if (setting.percentage < 0f) setting.percentage = 0f;
            if (setting.percentage > 1f) setting.percentage = 1f;

            return setting;
        }


        /// <summary>
        /// Handles saving and loading of all settings fields for this save game.
        /// Called by RimWorld when the game is saved or loaded.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref modEnabled, "modEnabled_perSave", true);
            Scribe_Values.Look(ref enableDailyRefresh, "enableDailyRefresh_perSave", true);
            Scribe_Collections.Look(ref excludedPawnIDs, "excludedPawnIDs_perSave", LookMode.Value);
            Scribe_Collections.Look(ref excludedWorkTypeDefNames, "excludedWorkTypeDefNames_perSave", LookMode.Value);
            Scribe_Collections.Look(ref workSettings, "workSettings_perSave", LookMode.Value, LookMode.Deep,
                ref workSettingsKeysWorkingList, ref workSettingsValuesWorkingList);

            // --- Post Load Initialization ---
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workSettings == null) workSettings = new Dictionary<string, WorkSettingValues>();
                if (excludedPawnIDs == null) excludedPawnIDs = new List<string>();
                if (excludedWorkTypeDefNames == null) excludedWorkTypeDefNames = new List<string>();
            }
        }
    }
}