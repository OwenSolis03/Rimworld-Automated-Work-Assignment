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
            if (Settings == null)
            {
                Widgets.Label(inRect, "Error: Mod settings could not be loaded. Please check logs.");
                Log.ErrorOnce("[AutoWork] Settings object is null in DoSettingsWindowContents. Cannot draw settings.", 91827364);
                return;
            }

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            try // General settings elements
            {
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
                if (listingStandard.ButtonText("AWA_ManageExclusionsButton".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_ManageExclusions(Settings));
                }
                listingStandard.GapLine(12f);
                listingStandard.Label("AWA_DesiredPawnsLabel".Translate());
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception drawing general settings elements: {ex}");
                listingStandard.Label($"Error drawing general settings: {ex.Message}");
            }

            // --- ScrollView Setup ---
            Rect outRect = default;
            Rect viewRect = default;
            List<WorkTypeDef> relevantWorkTypes = null;
            const float rowHeight = 50f;

            try // ScrollView and WorkType list preparation
            {
                float currentYPos = listingStandard.CurHeight;
                float availableHeight = inRect.height - currentYPos - 30f;
                float remainingHeight = Mathf.Max(100f, availableHeight);
                outRect = new Rect(inRect.x, currentYPos, inRect.width, remainingHeight);

                if (cachedRelevantWorkTypes == null)
                {
                    cachedRelevantWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                        .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                        .OrderBy(wtd => wtd.labelShort)
                        .ToList();
                }
                relevantWorkTypes = cachedRelevantWorkTypes;

                float totalContentHeight = relevantWorkTypes.Count * rowHeight;
                float viewRectWidth = Mathf.Max(0f, outRect.width - 16f);
                viewRect = new Rect(0f, 0f, viewRectWidth, totalContentHeight);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception preparing ScrollView or WorkType list: {ex}");
                listingStandard.Label($"Error preparing work type list: {ex.Message}");
                listingStandard.End();
                return;
            }

            // Begin ScrollView
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Listing_Standard scrollListing = new Listing_Standard(GameFont.Small);
            scrollListing.Begin(viewRect);

            if (relevantWorkTypes != null)
            {
                foreach (WorkTypeDef workDef in relevantWorkTypes)
                {
                    try // Handle drawing individual row
                    {
                        if (workDef == null) continue;

                        string defName = workDef.defName;
                        WorkSettingValues currentSetting = Settings.GetWorkSetting(defName);
                        if (currentSetting == null)
                        {
                            Log.ErrorOnce($"[AutoWork] GetWorkSetting returned null for {defName}!", defName.GetHashCode());
                            scrollListing.Label($"Error: Null settings for {workDef.labelShort}");
                            scrollListing.Gap(rowHeight - 30f);
                            continue;
                        }

                        // --- Draw Row Elements ---
                        Rect rowRect = scrollListing.GetRect(rowHeight - scrollListing.verticalSpacing);
                        float currentX = rowRect.x;

                        // 1. Work type label
                        float labelWidth = rowRect.width * 0.35f;
                        Rect labelRect = new Rect(currentX, rowRect.y, labelWidth, 30f);
                        Widgets.Label(labelRect, workDef.labelShort.CapitalizeFirst());
                        currentX += labelWidth;

                        // 2. Exclude Task Checkbox
                        float checkboxSize = Widgets.CheckboxSize;
                        float checkboxPadding = 5f;
                        Rect excludeCheckboxRect = new Rect(currentX + checkboxPadding, rowRect.y + (30f - checkboxSize) / 2f, checkboxSize, checkboxSize);
                        currentX += checkboxSize + checkboxPadding * 2;

                        bool isWorkTypeExcluded = Settings.excludedWorkTypeDefNames?.Contains(defName) ?? false;
                        bool checkboxState = isWorkTypeExcluded;
                        Widgets.Checkbox(excludeCheckboxRect.position, ref checkboxState, checkboxSize);
                        TooltipHandler.TipRegion(excludeCheckboxRect, "AWA_ExcludeTaskTooltip".Translate());

                        if (checkboxState != isWorkTypeExcluded)
                        {
                            if (checkboxState)
                            {
                                if (Settings.excludedWorkTypeDefNames == null) Settings.excludedWorkTypeDefNames = new List<string>();
                                if (!Settings.excludedWorkTypeDefNames.Contains(defName)) Settings.excludedWorkTypeDefNames.Add(defName);
                            }
                            else
                            {
                                Settings.excludedWorkTypeDefNames?.Remove(defName);
                            }
                        }
                        
                        const float spacing = 10f; // Define spacing between input groups

                        // Calculate remaining width for count/priority controls
                        float remainingRowWidth = rowRect.width - currentX;
                        // Divide remaining space, subtracting spacing between groups
                        float inputGroupWidth = (remainingRowWidth - spacing) / 2f;

                        // 3. Count Input
                        const float countLabelWidth = 50f;
                        Rect countLabelRect = new Rect(currentX + spacing, rowRect.y, countLabelWidth, 30f);
                        Widgets.Label(countLabelRect, "AWA_CountLabel".Translate());
                        float countFieldWidth = Mathf.Max(20f, inputGroupWidth - countLabelWidth);
                        Rect countFieldRect = new Rect(countLabelRect.xMax, rowRect.y, countFieldWidth, 30f);
                        currentX = countFieldRect.xMax;

                        if (!countBuffers.ContainsKey(defName)) { countBuffers[defName] = currentSetting.count.ToString(); }
                        string countBuffer = countBuffers[defName];
                        int countBefore = currentSetting.count;
                        Widgets.TextFieldNumeric<int>(countFieldRect, ref currentSetting.count, ref countBuffer, 0, 999);
                        if (currentSetting.count != countBefore) { countBuffers[defName] = currentSetting.count.ToString(); }
                        else { if (countBuffer != currentSetting.count.ToString()) { countBuffers[defName] = currentSetting.count.ToString(); } }
                        if (currentSetting.count < 0) currentSetting.count = 0;

                        // 4. Priority Input
                        const float priorityLabelWidth = 60f;
                        Rect priorityLabelRect = new Rect(currentX + spacing, rowRect.y, priorityLabelWidth, 30f);
                        Widgets.Label(priorityLabelRect, "AWA_PriorityFieldLabel".Translate());
                        float priorityFieldWidth = Mathf.Max(20f, inputGroupWidth - priorityLabelWidth);
                        Rect priorityFieldRect = new Rect(priorityLabelRect.xMax, rowRect.y, priorityFieldWidth, 30f);

                        if (!priorityBuffers.ContainsKey(defName)) { priorityBuffers[defName] = currentSetting.priority.ToString(); }
                        string priorityBuffer = priorityBuffers[defName];
                        int priorityBefore = currentSetting.priority;
                        Widgets.TextFieldNumeric<int>(priorityFieldRect, ref currentSetting.priority, ref priorityBuffer, 1, 4);
                        if (currentSetting.priority != priorityBefore) { priorityBuffers[defName] = currentSetting.priority.ToString(); }
                        else { if (priorityBuffer != currentSetting.priority.ToString()) { priorityBuffers[defName] = currentSetting.priority.ToString(); } }
                        if (currentSetting.priority < 1) currentSetting.priority = 1;
                        if (currentSetting.priority > 4) currentSetting.priority = 4;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AutoWork] Exception drawing settings row for WorkTypeDef '{workDef?.defName ?? "NULL"}': {ex}");
                        scrollListing.Label($"Error processing {workDef?.labelShort ?? "Unknown WorkType"}");
                        scrollListing.Gap(rowHeight - 30f);
                    }
                } // End foreach WorkTypeDef
            } // End if relevantWorkTypes != null

            scrollListing.End();
            Widgets.EndScrollView();
            listingStandard.End();
        }

        /// <summary>
        /// Called by RimWorld when settings are to be saved.
        /// </summary>
        public override void WriteSettings() => base.WriteSettings();

        /* Example XML Key needed for the new checkbox tooltip:
         * <AWA_ExcludeTaskTooltip>Check to exclude this task from automatic assignment. The mod will ignore this task completely.</AWA_ExcludeTaskTooltip>
         */
    }
}