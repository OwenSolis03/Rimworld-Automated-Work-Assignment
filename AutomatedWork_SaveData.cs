using System.Collections.Generic;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// A RimWorld <see cref="GameComponent"/> dedicated to storing and managing all mod-related settings
    /// that are specific to a particular save game. This component ensures that the user's configuration
    /// for the Automated Work Assignment mod persists across saving and loading the game.
    /// It holds settings such as the overall mod enablement state for the save, the daily refresh toggle,
    /// detailed configurations for each work type, and lists of excluded pawns and work types.
    /// These settings are serialized (saved) and deserialized (loaded) using RimWorld's Scribe system
    /// within the <see cref="ExposeData"/> method.
    /// </summary>
    public class AutomatedWork_SaveData : GameComponent
    {
        // --- Settings Fields ---

        /// <summary>
        /// A boolean flag indicating whether the core logic of the Automated Work Assignment mod
        /// is active for this specific save game. If set to <c>false</c>, no automatic assignments
        /// or adjustments will occur in this save, regardless of other settings.
        /// Defaults to <c>true</c>. Persisted via <see cref="ExposeData"/>.
        /// </summary>
        public bool modEnabled = true;

        /// <summary>
        /// A boolean flag controlling whether the automatic daily refresh of work assignments
        /// (managed by <see cref="AutoAssign_GameComponent"/>) is enabled for this specific save game.
        /// If set to <c>false</c>, the daily check will not trigger <see cref="WorkAssigner.RefreshAssignments"/>.
        /// Defaults to <c>true</c>. Persisted via <see cref="ExposeData"/>.
        /// </summary>
        public bool enableDailyRefresh = true;

        /// <summary>
        /// Stores the detailed configuration for each individual work type (<see cref="WorkTypeDef"/>)
        /// within this save game. The dictionary uses the <see cref="WorkTypeDef.defName"/> as the key
        /// and a <see cref="WorkSettingValues"/> object containing the count, priority, percentage, and mode
        /// for that work type as the value. Initialized as an empty dictionary if null during loading.
        /// Persisted via <see cref="ExposeData"/>. Use <see cref="GetWorkSetting"/> to access or create entries.
        /// </summary>
        public Dictionary<string, WorkSettingValues> workSettings = new Dictionary<string, WorkSettingValues>();

        /// <summary>
        /// A list containing the unique <see cref="Thing.ThingID"/> strings of pawns that should be
        /// completely ignored by the automatic work assignment logic in this save game. Pawns in this list
        /// will not have their work priorities modified by the mod. Initialized as an empty list if null
        /// during loading. Managed via the <see cref="Dialog_ManageExclusions"/> window.
        /// Persisted via <see cref="ExposeData"/>.
        /// </summary>
        public List<string> excludedPawnIDs = new List<string>();

        /// <summary>
        /// A list containing the <see cref="Def.defName"/> strings of <see cref="WorkTypeDef"/>s that should be
        /// completely excluded from the automatic assignment process in this save game. No assignments will be
        /// made for work types whose defNames appear in this list. Initialized as an empty list if null
        /// during loading. Managed via the mod's settings interface.
        /// Persisted via <see cref="ExposeData"/>.
        /// </summary>
        public List<string> excludedWorkTypeDefNames = new List<string>();

        // --- Private working lists required by Scribe for dictionary serialization ---

        /// <summary>
        /// Internal temporary list utilized exclusively by the RimWorld Scribe system
        /// (<see cref="Scribe_Collections.Look{K, V}(ref Dictionary{K, V}, string, LookMode, LookMode, ref List{K}, ref List{V})"/>)
        /// during the process of saving or loading the <see cref="workSettings"/> dictionary. It holds the keys
        /// (<c>string</c> representing <see cref="WorkTypeDef.defName"/>) temporarily during serialization/deserialization.
        /// This field should not be accessed or modified directly by mod logic.
        /// </summary>
        private List<string> workSettingsKeysWorkingList;

        /// <summary>
        /// Internal temporary list utilized exclusively by the RimWorld Scribe system
        /// (<see cref="Scribe_Collections.Look{K, V}(ref Dictionary{K, V}, string, LookMode, LookMode, ref List{K}, ref List{V})"/>)
        /// during the process of saving or loading the <see cref="workSettings"/> dictionary. It holds the values
        /// (<see cref="WorkSettingValues"/>) temporarily during serialization/deserialization.
        /// This field should not be accessed or modified directly by mod logic.
        /// </summary>
        private List<WorkSettingValues> workSettingsValuesWorkingList;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomatedWork_SaveData"/> component.
        /// This constructor is mandatory for all <see cref="GameComponent"/> subclasses and is called
        /// by the RimWorld engine when associating the component with a <see cref="Verse.Game"/> instance.
        /// </summary>
        /// <param name="game">The current <see cref="Verse.Game"/> instance this component is being added to.</param>
        public AutomatedWork_SaveData(Game game) { }

        /// <summary>
        /// Retrieves the specific <see cref="WorkSettingValues"/> for a given work type, identified by its defName,
        /// within the context of this save game. If settings for the specified <paramref name="workTypeDefName"/>
        /// do not already exist in the <see cref="workSettings"/> dictionary, this method creates a new
        /// <see cref="WorkSettingValues"/> instance with default values, adds it to the dictionary, and then returns it.
        /// It also performs safety checks: initializes the <see cref="workSettings"/> dictionary if it happens to be null,
        /// and clamps the retrieved/created setting's values (priority, count, percentage) to valid ranges before returning.
        /// </summary>
        /// <param name="workTypeDefName">The unique definition name (<c>defName</c>) of the <see cref="WorkTypeDef"/>
        /// for which settings are requested.</param>
        /// <returns>The existing or newly created <see cref="WorkSettingValues"/> instance for the specified work type,
        /// ensuring its values are within valid bounds.</returns>
        public WorkSettingValues GetWorkSetting(string workTypeDefName)
        {
            // Safety check: Ensure the dictionary exists before attempting access.
            if (workSettings == null)
            {
                Log.Warning("[AutoWork] SaveData: workSettings dictionary was null in GetWorkSetting. Initializing.");
                workSettings = new Dictionary<string, WorkSettingValues>();
            }

            // Attempt to retrieve the setting. If not found, create and add a default one.
            if (!workSettings.TryGetValue(workTypeDefName, out WorkSettingValues setting))
            {
                setting = new WorkSettingValues(); // Create with defaults defined in WorkSettingValues
                workSettings.Add(workTypeDefName, setting);
            }

            // Ensure values are within logical limits after retrieval or creation.
            // Note: Clamping is also done during loading in WorkSettingValues.ExposeData,
            // but this provides an extra layer of safety for runtime access.
            if (setting.priority < 1) setting.priority = 1;
            if (setting.priority > 4) setting.priority = 4; // RimWorld priorities are 1-4
            if (setting.count < 0) setting.count = 0; // Cannot assign negative pawns
            if (setting.percentage < 0f) setting.percentage = 0f; // Percentage cannot be negative
            if (setting.percentage > 1f) setting.percentage = 1f; // Percentage cannot exceed 100%

            return setting;
        }


        /// <summary>
        /// Implements the core save/load logic for this <see cref="GameComponent"/>. This method is automatically
        /// called by the RimWorld engine during the game saving and loading procedures. It uses the
        /// <see cref="Verse.Scribe"/> system (specifically <see cref="Scribe_Values"/> and <see cref="Scribe_Collections"/>)
        /// to serialize (write) the values of all public settings fields (<see cref="modEnabled"/>,
        /// <see cref="enableDailyRefresh"/>, <see cref="workSettings"/>, <see cref="excludedPawnIDs"/>,
        /// <see cref="excludedWorkTypeDefNames"/>) to the save file, or deserialize (read) them back when loading.
        /// It also includes a post-load initialization step (<c>if (Scribe.mode == LoadSaveMode.PostLoadInit)</c>)
        /// to ensure that collection fields (dictionaries and lists) are initialized to empty collections if they
        /// were null after loading (e.g., if loading a save from before these fields were added).
        /// </summary>
        public override void ExposeData()
        {
            // Always call the base method first for GameComponent's own potential data handling.
            base.ExposeData();

            // Save/Load individual boolean flags with default values if not found in save data.
            Scribe_Values.Look(ref modEnabled, "modEnabled_perSave", true);
            Scribe_Values.Look(ref enableDailyRefresh, "enableDailyRefresh_perSave", true);

            // Save/Load the list of excluded pawn IDs. LookMode.Value saves the string IDs directly.
            Scribe_Collections.Look(ref excludedPawnIDs, "excludedPawnIDs_perSave", LookMode.Value);

            // Save/Load the list of excluded work type definition names.
            Scribe_Collections.Look(ref excludedWorkTypeDefNames, "excludedWorkTypeDefNames_perSave", LookMode.Value);

            // Save/Load the dictionary of work settings.
            // LookMode.Value for keys (string defNames), LookMode.Deep for values (WorkSettingValues objects).
            // Requires temporary lists for the Scribe system to handle dictionary serialization.
            Scribe_Collections.Look(ref workSettings, "workSettings_perSave", LookMode.Value, LookMode.Deep,
                ref workSettingsKeysWorkingList, ref workSettingsValuesWorkingList);

            // --- Post Load Initialization ---
            // This block executes only after all data has been loaded (LoadingVars phase is complete).
            // It's crucial for ensuring collections are not null if they weren't present in the save file
            // (e.g., loading an older save after adding a new collection field).
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // If any collection is null after loading, initialize it as a new empty collection.
                if (workSettings == null) workSettings = new Dictionary<string, WorkSettingValues>();
                if (excludedPawnIDs == null) excludedPawnIDs = new List<string>();
                if (excludedWorkTypeDefNames == null) excludedWorkTypeDefNames = new List<string>();

                // Note: Further validation or clamping of loaded data can be done here if needed,
                // although WorkSettingValues.ExposeData already handles clamping for its own fields.
            }
        }
    }
}