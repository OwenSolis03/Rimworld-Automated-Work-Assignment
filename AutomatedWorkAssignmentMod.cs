using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Serves as the primary entry point and controller for the Automated Work Assignment mod within RimWorld's modding framework.
    /// This class inherits from <see cref="Verse.Mod"/> and is responsible for:
    /// 1. Providing the mod's entry in the game's mod settings menu (<see cref="SettingsCategory"/>).
    /// 2. Drawing the user interface for configuring the mod's settings (<see cref="DoSettingsWindowContents"/>).
    /// Note: Global mod settings (like those saved via <see cref="Mod.WriteSettings"/>) are not used by this mod;
    /// all configurations are stored per-save game using the <see cref="AutomatedWork_SaveData"/> component.
    /// </summary>
    public class AutomatedWorkAssignmentMod : Mod
    {
        /// <summary>
        /// Provides convenient static access to the instance of <see cref="AutomatedWork_SaveData"/>
        /// associated with the currently active game save. This component holds all per-save settings for the mod.
        /// Returns <c>null</c> if there is no game currently loaded (<c>Current.Game</c> is null).
        /// The settings UI (<see cref="DoSettingsWindowContents"/>) relies on this property to access and modify settings.
        /// </summary>
        internal static AutomatedWork_SaveData CurrentData => Current.Game?.GetComponent<AutomatedWork_SaveData>();

        /// <summary>
        /// Stores the current vertical scroll position for the list of work types displayed within the mod settings window.
        /// This ensures that the user's scroll position is maintained when the settings window redraws.
        /// It is passed by reference to <see cref="Widgets.BeginScrollView"/>.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// A static cache holding the list of <see cref="WorkTypeDef"/>s that are considered relevant for display
        /// and configuration in the mod settings window. Relevant work types are those that have associated <see cref="WorkTags"/>
        /// (i.e., are not purely passive like Temperature).
        /// Caching this list avoids redundant filtering and sorting every time the settings window is drawn, improving performance.
        /// The cache is populated once when first needed and then reused. It's reset to null only if the game's Defs are reloaded.
        /// </summary>
        private static List<WorkTypeDef> cachedRelevantWorkTypes = null;

        /// <summary>
        /// Standard constructor for RimWorld mods. Called by the game when loading the mod.
        /// It receives the mod's content pack information.
        /// </summary>
        /// <param name="content">The <see cref="ModContentPack"/> object containing metadata and file paths for this mod.</param>
        public AutomatedWorkAssignmentMod(ModContentPack content) : base(content) { }

        /// <summary>
        /// Overrides the base method to provide the localized display name for this mod's section
        /// within RimWorld's main mod settings dialog.
        /// </summary>
        /// <returns>The translated string defined by the key "AWA_SettingsCategory" in the mod's language files.</returns>
        public override string SettingsCategory() => "AWA_SettingsCategory".Translate();

        /// <summary>
        /// This method is called by RimWorld to draw the user interface controls inside the mod's dedicated settings window.
        /// It handles layout, drawing labels, checkboxes, buttons, sliders, and the scrollable list of work types.
        /// All interactions within this UI directly read from and modify the settings stored in the <see cref="CurrentData"/>
        /// (<see cref="AutomatedWork_SaveData"/>) instance for the currently loaded save game.
        /// </summary>
        /// <param name="inRect">The <see cref="Rect"/> representing the total available drawing area for the settings window content.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Attempt to get the per-save data component.
            var saveData = CurrentData;
            // If no save is loaded, display a message and exit, as settings are per-save.
            if (saveData == null)
            {
                // Use standard RimWorld widget to draw a label centered in the provided rectangle.
                Widgets.Label(inRect, "AWA_LoadSaveFirst".Translate());
                return; // Stop further drawing as there's no data context.
            }

            // Use RimWorld's helper class for standard layout elements (labels, checkboxes, gaps).
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect); // Start drawing within the provided rectangle.

            // --- Section 1: General Settings and Buttons ---
            try // Wrap UI sections in try-catch to prevent errors in one part from breaking the entire window.
            {
                // Checkbox to enable/disable the entire mod's logic for this save. Linked to saveData.modEnabled.
                listingStandard.CheckboxLabeled("AWA_EnableModLabel".Translate(), ref saveData.modEnabled, "AWA_EnableModTooltip".Translate());
                // Checkbox to enable/disable the automatic daily refresh assignment logic. Linked to saveData.enableDailyRefresh.
                listingStandard.CheckboxLabeled("AWA_EnableDailyRefreshLabel".Translate(), ref saveData.enableDailyRefresh, "AWA_EnableDailyRefreshTooltip".Translate());
                listingStandard.GapLine(12f); // Draw a visual separator line with padding.
                // Button that, when clicked, opens the pawn exclusion management dialog.
                if (listingStandard.ButtonText("AWA_ManageExclusionsButton".Translate()))
                {
                    // Create and add the custom dialog window to the game's window stack.
                    Find.WindowStack.Add(new Dialog_ManageExclusions(saveData));
                }
                listingStandard.GapLine(12f); // Another separator.
                // Label introducing the work type settings section.
                listingStandard.Label("AWA_DesiredPawnsLabel".Translate());
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception drawing general settings: {ex}"); }

            // --- Calculate Max Value for Fixed Count Slider ---
            int maxPawnCountForSlider = 10; // Default fallback value.
            try
            {
                // Calculate the number of colonists eligible for work assignment.
                int eligibleCount = WorkAssigner.GetEligibleColonistCount(saveData);
                // Use the eligible count as the maximum for the fixed count slider, ensuring it's at least 0.
                maxPawnCountForSlider = Mathf.Max(0, eligibleCount);
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception getting eligible count: {ex}"); }


            // --- Section 2: Scrollable List of Work Type Settings ---
            Rect outRect = default; // The outer visible rectangle for the scroll view.
            Rect viewRect = default; // The inner, potentially larger rectangle containing all the content.
            List<WorkTypeDef> relevantWorkTypes = null; // The list of work types to display.
            const float rowHeight = 45f; // Fixed height for each row in the list for consistent layout.

            try // Setup the scroll view dimensions and content list.
            {
                // Calculate the rectangle available for the scroll view below the general settings.
                float currentYPos = listingStandard.CurHeight; // Get Y position after drawing general settings.
                float availableHeight = inRect.height - currentYPos - 30f; // Subtract space for bottom margin.
                float remainingHeight = Mathf.Max(100f, availableHeight); // Ensure a minimum height.
                outRect = new Rect(inRect.x, currentYPos, inRect.width, remainingHeight);

                // Populate the list of work types if the cache is empty.
                if (cachedRelevantWorkTypes == null)
                {
                    cachedRelevantWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                        .Where(wtd => wtd != null && wtd.workTags != WorkTags.None) // Filter out nulls and passive work types.
                        .OrderBy(wtd => wtd.labelShort) // Sort alphabetically by short label.
                        .ToList();
                }
                relevantWorkTypes = cachedRelevantWorkTypes; // Use the cached or newly populated list.

                // Calculate the total height required to display all work types.
                float totalContentHeight = relevantWorkTypes.Count * rowHeight;
                // Calculate the width of the inner view rect, accounting for scrollbar width (16f).
                float viewRectWidth = Mathf.Max(0f, outRect.width - 16f);
                viewRect = new Rect(0f, 0f, viewRectWidth, totalContentHeight);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception preparing ScrollView: {ex}");
                listingStandard.Label($"Error preparing list: {ex.Message}"); // Display error in UI if setup fails.
                listingStandard.End(); return; // Stop drawing.
            }

            // --- Draw ScrollView Content ---
            // Begin the scrollable area. Uses the calculated rectangles and the scrollPosition state field.
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float currentY = viewRect.y; // Starting Y position within the scroll view's content area.

            if (relevantWorkTypes != null)
            {
                // Iterate through each relevant work type to draw its settings row.
                foreach (WorkTypeDef workDef in relevantWorkTypes)
                {
                    try // Wrap each row's drawing logic in a try-catch for resilience.
                    {
                        if (workDef == null) continue; // Skip if the workDef is somehow null.
                        string defName = workDef.defName; // Get the unique identifier.
                        // Retrieve the specific settings object for this work type from the save data.
                        // GetWorkSetting ensures a non-null object is returned (creates defaults if needed).
                        WorkSettingValues currentSetting = saveData.GetWorkSetting(defName);
                        if (currentSetting == null) continue; // Should not happen due to GetWorkSetting logic, but safety first.

                        // --- Define the Row Layout ---
                        Rect rowRect = new Rect(viewRect.x, currentY, viewRect.width, rowHeight); // Overall row bounds.
                        float currentX = rowRect.x; // Track horizontal position for placing elements.
                        float controlAreaHeight = 30f; // Standard height for interactive elements like sliders/buttons.
                        // Calculate Y position to vertically center controls within the row height.
                        float rowCenterY = rowRect.y + (rowHeight - controlAreaHeight) / 2f;

                        // --- Draw Controls within the Row ---

                        // 1. Work Type Label: Display the short name (e.g., "Cook", "Mine").
                        float labelWidth = viewRect.width * 0.25f; // Allocate 25% of width.
                        Rect labelRect = new Rect(currentX, rowCenterY, labelWidth, controlAreaHeight);
                        Widgets.Label(labelRect, workDef.labelShort.CapitalizeFirst());
                        currentX += labelWidth; // Move X position past the label.

                        // 2. Include Task Checkbox: Toggles if this work type is managed by the mod.
                        float checkboxSize = Widgets.CheckboxSize;
                        float checkboxPadding = 5f; // Space around the checkbox.
                        Rect includeCheckboxRect = new Rect(currentX + checkboxPadding, rowCenterY + (controlAreaHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);
                        currentX += checkboxSize + checkboxPadding * 2; // Move X past checkbox and padding.

                        // Determine current state: It's included if *not* in the excluded list.
                        bool isIncluded = !(saveData.excludedWorkTypeDefNames?.Contains(defName) ?? false);
                        bool checkboxState = isIncluded; // Temporary state for the widget.
                        Widgets.Checkbox(includeCheckboxRect.position, ref checkboxState, checkboxSize); // Draw the checkbox.
                        TooltipHandler.TipRegion(includeCheckboxRect, "AWA_IncludeTaskTooltip".Translate()); // Add hover tooltip.

                        // If the checkbox state changed, update the underlying excluded list in saveData.
                        if (checkboxState != isIncluded)
                        {
                            if (checkboxState) // If now checked (meaning included)
                            {
                                saveData.excludedWorkTypeDefNames?.Remove(defName); // Remove from exclusion list.
                            }
                            else // If now unchecked (meaning excluded)
                            {
                                // Ensure the list exists before adding.
                                if (saveData.excludedWorkTypeDefNames == null) saveData.excludedWorkTypeDefNames = new List<string>();
                                // Add to exclusion list if not already present.
                                if (!saveData.excludedWorkTypeDefNames.Contains(defName)) saveData.excludedWorkTypeDefNames.Add(defName);
                            }
                        }

                        // 3. Mode Toggle Button: Switches between Fixed Count (#) and Percentage (%) modes.
                        float toggleWidth = 70f;
                        Rect toggleRect = new Rect(currentX + checkboxPadding, rowCenterY, toggleWidth, controlAreaHeight);
                        // Dynamically set button label based on current mode.
                        string toggleLabel = currentSetting.usePercentage ? "Mode: %" : "Mode: #";
                        if(Widgets.ButtonText(toggleRect, toggleLabel)) // Draw button and check if clicked.
                        {
                            // Invert the boolean flag on click.
                            currentSetting.usePercentage = !currentSetting.usePercentage;
                        }
                        TooltipHandler.TipRegion(toggleRect, "AWA_ToggleCountModeTooltip".Translate()); // Add hover tooltip.
                        currentX += toggleWidth + checkboxPadding; // Move X past the button.

                        // --- Sliders Area Calculation ---
                        // Calculate remaining width for the two sliders (Count/Percent and Priority).
                        float sliderAreaWidth = viewRect.width - currentX - 10f; // Subtract current pos and a small margin.
                        float sliderGroupWidth = sliderAreaWidth / 2f; // Divide remaining space equally.
                        const float spacing = 5f; // Small space between sliders.

                        // 4. Count or Percentage Slider (Conditional based on 'usePercentage' mode)
                        Rect countPercentGroupRect = new Rect(currentX + spacing, rowCenterY, sliderGroupWidth - spacing, controlAreaHeight);
                        if (currentSetting.usePercentage) // If in percentage mode...
                        {
                            // Draw a horizontal slider for percentage (0.0 to 1.0).
                            currentSetting.percentage = Widgets.HorizontalSlider(
                                countPercentGroupRect, // Bounding rectangle.
                                currentSetting.percentage, // Current value.
                                0f, 1f, // Min and Max values.
                                true, // Draw background fill.
                                // Display current value formatted as percentage in the slider label.
                                "AWA_PercentageLabel".Translate(currentSetting.percentage.ToStringPercent()),
                                roundTo: 0.01f // Snap slider value to nearest 0.01 (1%).
                            );
                        }
                        else // If in fixed count mode...
                        {
                            // Clamp the current count to valid range (0 to max eligible pawns) before passing to slider.
                            float tempCount = Mathf.Clamp(currentSetting.count, 0, maxPawnCountForSlider);
                            // Draw a horizontal slider for fixed count (0 to max eligible pawns).
                            tempCount = Widgets.HorizontalSlider(
                                countPercentGroupRect, // Bounding rectangle.
                                tempCount, // Clamped current value.
                                0f, (float)maxPawnCountForSlider, // Min and Max values.
                                true, // Draw background fill.
                                // Display current fixed count in the slider label.
                                "AWA_FixedCountLabel".Translate(currentSetting.count),
                                roundTo: 1f // Snap slider value to nearest integer.
                            );
                            // Update the setting only if the integer value actually changed.
                            if((int)tempCount != currentSetting.count) { currentSetting.count = (int)tempCount; }
                        }
                        currentX += sliderGroupWidth; // Move X past the first slider group.

                        // 5. Priority Slider: Sets the target priority (1-4) for assigned pawns.
                        Rect priorityGroupRect = new Rect(currentX + spacing, rowCenterY, sliderGroupWidth - spacing, controlAreaHeight);
                        // Clamp current priority to valid RimWorld range (1-4) before passing to slider.
                        float tempPriority = Mathf.Clamp(currentSetting.priority, 1, 4);
                        // Draw a horizontal slider for priority (1 to 4).
                        tempPriority = Widgets.HorizontalSlider(
                            priorityGroupRect, // Bounding rectangle.
                            tempPriority, // Clamped current value.
                            1f, 4f, // Min and Max values (RimWorld priorities).
                            true, // Draw background fill.
                            // Display current priority in the slider label.
                            "AWA_PriorityFieldLabel".Translate(currentSetting.priority),
                            roundTo: 1f // Snap slider value to nearest integer.
                        );
                        // Update the setting only if the integer value actually changed.
                        if((int)tempPriority != currentSetting.priority) { currentSetting.priority = (int)tempPriority; }

                    }
                    catch (Exception ex) // Catch errors specific to drawing this single row.
                    {
                        Log.Error($"[AutoWork] Exception drawing settings row for WorkTypeDef '{workDef?.defName ?? "NULL"}': {ex}");
                        // Draw an error message in place of the row if something goes wrong.
                        Rect errorRect = new Rect(viewRect.x, currentY, viewRect.width, 30f);
                        Widgets.Label(errorRect, $"Error processing {workDef?.labelShort ?? "Unknown WorkType"}");
                    }
                    finally // Ensure Y position is incremented even if an error occurred.
                    {
                        currentY += rowHeight; // Move Y down to the start of the next row.
                    }
                } // End foreach loop through work types.
            } // End if (relevantWorkTypes != null)

            // --- End ScrollView ---
            Widgets.EndScrollView(); // Must be called to finalize the scroll view drawing.
            listingStandard.End(); // Finalize the main layout listing.
        }

        /// <summary>
        /// Overrides the base <see cref="Mod.WriteSettings"/> method.
        /// This method is intentionally left empty because this mod does *not* use RimWorld's global mod settings system.
        /// All settings are managed on a per-save-game basis using the <see cref="AutomatedWork_SaveData"/> component,
        /// which handles its own saving and loading via its <see cref="AutomatedWork_SaveData.ExposeData"/> method
        /// when the game itself is saved or loaded.
        /// </summary>
        public override void WriteSettings() { /* Intentionally empty - Settings are per-save via AutomatedWork_SaveData */ }

    }
}