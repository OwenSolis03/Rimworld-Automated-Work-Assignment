using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Main class for the Automated Work Assignment mod. Handles settings UI and initialization.
    /// </summary>
    public class AutomatedWorkAssignmentMod : Mod
    {
        /// <summary> Static reference to the mod's per-save data instance. </summary>
        internal static AutomatedWork_SaveData CurrentData => Current.Game?.GetComponent<AutomatedWork_SaveData>();

        /// <summary> Stores the current scroll position of the work type settings list. </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary> Cached list of relevant WorkTypeDefs. </summary>
        private static List<WorkTypeDef> cachedRelevantWorkTypes = null;

        /// <summary> Constructor for the mod. </summary>
        public AutomatedWorkAssignmentMod(ModContentPack content) : base(content) { }

        /// <summary> Provides the title for the mod's settings category. </summary>
        public override string SettingsCategory() => "AWA_SettingsCategory".Translate();

        /// <summary> Draws the content of the mod settings window. </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var saveData = CurrentData;
            if (saveData == null)
            {
                Widgets.Label(inRect, "AWA_LoadSaveFirst".Translate());
                return;
            }

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // --- General Settings and Buttons ---
            try
            {
                listingStandard.CheckboxLabeled("AWA_EnableModLabel".Translate(), ref saveData.modEnabled, "AWA_EnableModTooltip".Translate());
                listingStandard.CheckboxLabeled("AWA_EnableDailyRefreshLabel".Translate(), ref saveData.enableDailyRefresh, "AWA_EnableDailyRefreshTooltip".Translate());
                listingStandard.GapLine(12f);
                if (listingStandard.ButtonText("AWA_ManageExclusionsButton".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_ManageExclusions(saveData));
                }
                listingStandard.GapLine(12f);
                listingStandard.Label("AWA_DesiredPawnsLabel".Translate());
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception drawing general settings: {ex}"); }

            // --- Calculate Max Value for Fixed Count Slider ---
            int maxPawnCountForSlider = 10;
            try
            {
                int eligibleCount = WorkAssigner.GetEligibleColonistCount(saveData);
                maxPawnCountForSlider = Mathf.Max(0, eligibleCount);
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception getting eligible count: {ex}"); }

            // --- ScrollView Setup ---
            Rect outRect = default;
            Rect viewRect = default;
            List<WorkTypeDef> relevantWorkTypes = null;
            const float rowHeight = 45f;

            try
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
                Log.Error($"[AutoWork] Exception preparing ScrollView: {ex}");
                listingStandard.Label($"Error preparing list: {ex.Message}");
                listingStandard.End(); return;
            }

            // --- Draw ScrollView Content ---
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float currentY = viewRect.y;

            if (relevantWorkTypes != null)
            {
                foreach (WorkTypeDef workDef in relevantWorkTypes)
                {
                    try
                    {
                        if (workDef == null) continue;
                        string defName = workDef.defName;
                        WorkSettingValues currentSetting = saveData.GetWorkSetting(defName);
                        if (currentSetting == null) continue;

                        Rect rowRect = new Rect(viewRect.x, currentY, viewRect.width, rowHeight);
                        float currentX = rowRect.x;
                        float controlAreaHeight = 30f;
                        float rowCenterY = rowRect.y + (rowHeight - controlAreaHeight) / 2f;

                        // 1. Work type label
                        float labelWidth = viewRect.width * 0.25f;
                        Rect labelRect = new Rect(currentX, rowCenterY, labelWidth, controlAreaHeight);
                        Widgets.Label(labelRect, workDef.labelShort.CapitalizeFirst());
                        currentX += labelWidth;

                        // 2. Include Task Checkbox (Logic Inverted)
                        float checkboxSize = Widgets.CheckboxSize;
                        float checkboxPadding = 5f;
                        Rect includeCheckboxRect = new Rect(currentX + checkboxPadding, rowCenterY + (controlAreaHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);
                        currentX += checkboxSize + checkboxPadding * 2;

                        bool isIncluded = !(saveData.excludedWorkTypeDefNames?.Contains(defName) ?? false);
                        bool checkboxState = isIncluded;
                        Widgets.Checkbox(includeCheckboxRect.position, ref checkboxState, checkboxSize);
                        TooltipHandler.TipRegion(includeCheckboxRect, "AWA_IncludeTaskTooltip".Translate());

                        if (checkboxState != isIncluded)
                        {
                            if (checkboxState)
                            {
                                saveData.excludedWorkTypeDefNames?.Remove(defName);
                            }
                            else
                            {
                                if (saveData.excludedWorkTypeDefNames == null) saveData.excludedWorkTypeDefNames = new List<string>();
                                if (!saveData.excludedWorkTypeDefNames.Contains(defName)) saveData.excludedWorkTypeDefNames.Add(defName);
                            }
                        }

                        // 3. Mode Toggle Button
                        float toggleWidth = 70f;
                        Rect toggleRect = new Rect(currentX + checkboxPadding, rowCenterY, toggleWidth, controlAreaHeight);
                        string toggleLabel = currentSetting.usePercentage ? "Mode: %" : "Mode: #";
                        if(Widgets.ButtonText(toggleRect, toggleLabel))
                        {
                            currentSetting.usePercentage = !currentSetting.usePercentage;
                        }
                        TooltipHandler.TipRegion(toggleRect, "AWA_ToggleCountModeTooltip".Translate());
                        currentX += toggleWidth + checkboxPadding;

                        // Calculate remaining width for sliders
                        float sliderAreaWidth = viewRect.width - currentX - 10f;
                        float sliderGroupWidth = sliderAreaWidth / 2f;
                        const float spacing = 5f;

                        // 4. Count or Percentage Slider
                        Rect countPercentGroupRect = new Rect(currentX + spacing, rowCenterY, sliderGroupWidth - spacing, controlAreaHeight);
                        if (currentSetting.usePercentage)
                        {
                            currentSetting.percentage = Widgets.HorizontalSlider(
                                countPercentGroupRect, currentSetting.percentage, 0f, 1f, true,
                                "AWA_PercentageLabel".Translate(currentSetting.percentage.ToStringPercent()), roundTo: 0.01f
                            );
                        }
                        else
                        {
                            float tempCount = Mathf.Clamp(currentSetting.count, 0, maxPawnCountForSlider);
                            tempCount = Widgets.HorizontalSlider(
                                countPercentGroupRect, tempCount, 0f, (float)maxPawnCountForSlider, true,
                                "AWA_FixedCountLabel".Translate(currentSetting.count), roundTo: 1f
                            );
                            if((int)tempCount != currentSetting.count) { currentSetting.count = (int)tempCount; }
                        }
                        currentX += sliderGroupWidth;

                        // 5. Priority Slider
                        Rect priorityGroupRect = new Rect(currentX + spacing, rowCenterY, sliderGroupWidth - spacing, controlAreaHeight);
                        float tempPriority = Mathf.Clamp(currentSetting.priority, 1, 4);
                        tempPriority = Widgets.HorizontalSlider(
                            priorityGroupRect, tempPriority, 1f, 4f, true,
                            "AWA_PriorityFieldLabel".Translate(currentSetting.priority), roundTo: 1f
                        );
                        if((int)tempPriority != currentSetting.priority) { currentSetting.priority = (int)tempPriority; }

                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AutoWork] Exception drawing settings row for WorkTypeDef '{workDef?.defName ?? "NULL"}': {ex}");
                        Rect errorRect = new Rect(viewRect.x, currentY, viewRect.width, 30f);
                        Widgets.Label(errorRect, $"Error processing {workDef?.labelShort ?? "Unknown WorkType"}");
                    }
                    finally
                    {
                        currentY += rowHeight;
                    }
                }
            }

            // --- End ScrollView ---
            Widgets.EndScrollView();
            listingStandard.End();
        }

        /// <summary> Called by RimWorld when settings are to be saved. </summary>
        public override void WriteSettings() { /* No longer used for per-save settings */ }

    }
}