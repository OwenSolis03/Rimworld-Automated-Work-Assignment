using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Main class for the Automated Work Assignment mod. Handles settings UI and initialization.
    /// Inherits from Verse.Mod to integrate with RimWorld's mod system.
    /// </summary>
    public class AutomatedWorkAssignmentMod : Mod
    {
        /// <summary>
        /// Static reference to the mod's settings instance.
        /// </summary>
        public static AutomatedWorkSettings Settings;

        /// <summary>
        /// Buffer dictionary to store the string representation of the desired pawn count for each work type.
        /// Used by TextFieldNumeric for the count input.
        /// </summary>
        private Dictionary<string, string> countBuffers = new Dictionary<string, string>();

        /// <summary>
        /// Buffer dictionary to store the string representation of the desired priority for each work type.
        /// Used by TextFieldNumeric for the priority input.
        /// </summary>
        private Dictionary<string, string> priorityBuffers = new Dictionary<string, string>();

        /// <summary>
        /// Stores the current scroll position of the work type settings list.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Cached list of relevant WorkTypeDefs to avoid querying DefDatabase repeatedly in DoSettingsWindowContents.
        /// </summary>
        private static List<WorkTypeDef> cachedRelevantWorkTypes = null;

        /// <summary>
        /// Constructor for the mod. Loads the settings.
        /// Includes basic exception handling for settings loading.
        /// </summary>
        /// <param name="content">The mod content pack.</param>
        public AutomatedWorkAssignmentMod(ModContentPack content) : base(content)
        {
            try
            {
                Settings = GetSettings<AutomatedWorkSettings>();
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception while loading settings in constructor: {ex}");
                // Consider initializing Settings to a default instance if loading fails
                // Settings = new AutomatedWorkSettings();
            }
        }

        /// <summary>
        /// Provides the title for the mod's settings category in the game's mod settings list.
        /// </summary>
        /// <returns>The translated settings category title.</returns>
        public override string SettingsCategory() => "AWA_SettingsCategory".Translate();

        /// <summary>
        /// Draws the content of the mod settings window.
        /// Includes exception handling for drawing UI elements and processing work types.
        /// </summary>
        /// <param name="inRect">The rectangle area available for drawing the settings content.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Ensure settings are loaded before proceeding
            if (Settings == null)
            {
                Widgets.Label(inRect, "Error: Mod settings could not be loaded. Please check logs.");
                Log.ErrorOnce("[AutoWork] Settings object is null in DoSettingsWindowContents. Cannot draw settings.", 91827364);
                return;
            }

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            try // General exception handling for the top-level settings elements
            {
                // --- General Mod Settings ---
                listingStandard.CheckboxLabeled(
                    "AWA_EnableModLabel".Translate(),
                    ref Settings.modEnabled,
                    "AWA_EnableModTooltip".Translate()
                );

                listingStandard.CheckboxLabeled(
                    "AWA_EnableDailyRefreshLabel".Translate(),
                    ref Settings.enableDailyRefresh,
                    "AWA_EnableDailyRefreshTooltip".Translate()
                );

                listingStandard.GapLine(12f);

                // --- Pawn Exclusions Button ---
                if (listingStandard.ButtonText("AWA_ManageExclusionsButton".Translate()))
                {
                    // Open the pawn exclusion dialog
                    Find.WindowStack.Add(new Dialog_ManageExclusions(Settings));
                }

                listingStandard.GapLine(12f);

                // --- Work Type Specific Settings Label ---
                listingStandard.Label("AWA_DesiredPawnsLabel".Translate());
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception drawing general settings elements: {ex}");
                listingStandard.Label($"Error drawing general settings: {ex.Message}");
                // Attempt to continue if possible, or end here if critical
            }

            // --- ScrollView Setup ---
            Rect outRect = default;
            Rect viewRect = default;
            List<WorkTypeDef> relevantWorkTypes = null;
            // Define rowHeight *before* the try block where it's used for calculations
            // and where it might be needed in error handling later.
            const float rowHeight = 50f; // Height allocated for each work type row (use const if it never changes)

            try // Exception handling for preparing the scroll view and work type list
            {
                // Calculate space remaining for the scroll view
                float currentYPos = listingStandard.CurHeight;
                // Ensure remainingHeight calculation doesn't result in negative values
                float availableHeight = inRect.height - currentYPos - 30f; // Adjusted for potential bottom margin
                float remainingHeight = Mathf.Max(100f, availableHeight); // Ensure a minimum height, prevent negative

                outRect = new Rect(inRect.x, currentYPos, inRect.width, remainingHeight);

                // --- Optimization: Cache relevant work types ---
                if (cachedRelevantWorkTypes == null)
                {
                    // Log.Message("[AutoWork] Caching relevant work types for settings..."); // Optional log
                    cachedRelevantWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                        .Where(wtd => wtd != null && wtd.workTags != WorkTags.None) // Added null check for safety
                        .OrderBy(wtd => wtd.labelShort)
                        .ToList();
                }
                relevantWorkTypes = cachedRelevantWorkTypes; // Use the cached list
                // ---------------------------------------------

                // Calculate dimensions for the scrollable content using the pre-defined rowHeight
                float totalContentHeight = relevantWorkTypes.Count * rowHeight;
                // Ensure viewRect width isn't negative if outRect width is too small
                float viewRectWidth = Mathf.Max(0f, outRect.width - 16f); // Subtract scrollbar width safely
                viewRect = new Rect(0f, 0f, viewRectWidth, totalContentHeight);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception preparing ScrollView or WorkType list: {ex}");
                listingStandard.Label($"Error preparing work type list: {ex.Message}");
                listingStandard.End(); // End listing here if setup failed
                return; // Exit drawing if we can't prepare the list/scrollview
            }


            // Begin ScrollView
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            Listing_Standard scrollListing = new Listing_Standard(GameFont.Small); // Use small font inside scroll view
            scrollListing.Begin(viewRect);

            // Iterate through each relevant work type to draw its settings row
            if (relevantWorkTypes != null) // Check list again just in case
            {
                foreach (WorkTypeDef workDef in relevantWorkTypes)
                {
                    // --- Add Exception Handling around each row's drawing logic ---
                    try
                    {
                        // Defensive check for null workDef, though unlikely from DefDatabase query
                        if (workDef == null) continue;

                        string defName = workDef.defName;
                        WorkSettingValues currentSetting = Settings.GetWorkSetting(defName);

                        // Check if GetWorkSetting returned null (shouldn't happen with current implementation but good practice)
                        if (currentSetting == null)
                        {
                            Log.ErrorOnce($"[AutoWork] GetWorkSetting returned null for {defName}!", defName.GetHashCode());
                            scrollListing.Label($"Error: Null settings for {workDef.labelShort}");
                            // Use the rowHeight defined outside the loop's try block
                            scrollListing.Gap(rowHeight - 30f); // Allocate space even on error to maintain layout
                            continue; // Skip this work type
                        }

                        // --- Draw Row Elements ---
                        Rect rowRect = scrollListing.GetRect(rowHeight - scrollListing.verticalSpacing);

                        // Work type label
                        Rect labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width * 0.4f, 30f);
                        Widgets.Label(labelRect, workDef.labelShort.CapitalizeFirst());

                        // Calculate dimensions for input fields
                        float controlWidth = rowRect.width * 0.25f;
                        float spacing = 10f;

                        // --- Count Input ---
                        Rect countLabelRect = new Rect(labelRect.xMax + spacing, rowRect.y, 50f, 30f);
                        Widgets.Label(countLabelRect, "AWA_CountLabel".Translate());
                        Rect countFieldRect = new Rect(countLabelRect.xMax, rowRect.y, controlWidth - 50f, 30f);

                        if (!countBuffers.ContainsKey(defName)) { countBuffers[defName] = currentSetting.count.ToString(); }
                        string countBuffer = countBuffers[defName];
                        int countBefore = currentSetting.count;
                        Widgets.TextFieldNumeric<int>(countFieldRect, ref currentSetting.count, ref countBuffer, 0, 999);
                        if (currentSetting.count != countBefore) { countBuffers[defName] = currentSetting.count.ToString(); }
                        else { if (countBuffer != currentSetting.count.ToString()) { countBuffers[defName] = currentSetting.count.ToString(); } }
                        if (currentSetting.count < 0) currentSetting.count = 0; // Clamping

                        // --- Priority Input ---
                        Rect priorityLabelRect = new Rect(countFieldRect.xMax + spacing, rowRect.y, 60f, 30f);
                        Widgets.Label(priorityLabelRect, "AWA_PriorityFieldLabel".Translate());
                        Rect priorityFieldRect = new Rect(priorityLabelRect.xMax, rowRect.y, controlWidth - 60f, 30f);

                        if (!priorityBuffers.ContainsKey(defName)) { priorityBuffers[defName] = currentSetting.priority.ToString(); }
                        string priorityBuffer = priorityBuffers[defName];
                        int priorityBefore = currentSetting.priority;
                        Widgets.TextFieldNumeric<int>(priorityFieldRect, ref currentSetting.priority, ref priorityBuffer, 1, 4);
                        if (currentSetting.priority != priorityBefore) { priorityBuffers[defName] = currentSetting.priority.ToString(); }
                        else { if (priorityBuffer != currentSetting.priority.ToString()) { priorityBuffers[defName] = currentSetting.priority.ToString(); } }
                        // Explicit Clamping (Defensive)
                        if (currentSetting.priority < 1) currentSetting.priority = 1;
                        if (currentSetting.priority > 4) currentSetting.priority = 4;
                    }
                    catch (Exception ex)
                    {
                        // Log the error with details about which work type failed
                        Log.Error($"[AutoWork] Exception drawing settings row for WorkTypeDef '{workDef?.defName ?? "NULL"}': {ex}");
                        // Optionally, draw an error message in place of the row
                        scrollListing.Label($"Error processing {workDef?.labelShort ?? "Unknown WorkType"}");
                        // Try to advance the listing using the rowHeight defined outside the loop's try block
                        scrollListing.Gap(rowHeight - 30f); // Approximate remaining height in the row
                    }
                    // --- End of Exception Handling for the row ---
                }
            } // End if (relevantWorkTypes != null)

            scrollListing.End();
            Widgets.EndScrollView(); // End ScrollView

            listingStandard.End(); // End main listing
        }

        /// <summary>
        /// Called by RimWorld when settings are to be saved.
        /// Currently relies on the base implementation which handles ModSettings saving.
        /// </summary>
        public override void WriteSettings() => base.WriteSettings();
    }
}