using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// The main entry point for the Automated Work Assignment mod.
    /// It handles the mod settings window, including the UI for configuring global settings,
    /// assignment modes, and specific work type priorities.
    /// </summary>
    public class AutomatedWorkAssignmentMod : Mod
    {
        /// <summary>
        /// Gets the save-specific data component for the current game.
        /// Returns null if no game is currently loaded.
        /// </summary>
        internal static AutomatedWork_SaveData CurrentData => Current.Game?.GetComponent<AutomatedWork_SaveData>();

        /// <summary>
        /// Current scroll position for the work type settings list.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Cached list of work types relevant to the mod (excluding work types with no work tags).
        /// </summary>
        private static List<WorkTypeDef> cachedRelevantWorkTypes = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomatedWorkAssignmentMod"/> class.
        /// </summary>
        /// <param name="content">The content pack associated with this mod.</param>
        public AutomatedWorkAssignmentMod(ModContentPack content) : base(content) { }

        /// <summary>
        /// Gets the name of the settings category to be displayed in the mod settings menu.
        /// </summary>
        /// <returns>The translated settings category name.</returns>
        public override string SettingsCategory() => "AWA_SettingsCategory".Translate();

        /// <summary>
        /// Draws the contents of the settings window.
        /// This includes general toggles, assignment mode selection, and the scrollable list
        /// of work type configurations.
        /// </summary>
        /// <param name="inRect">The rectangular area available for drawing the settings UI.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var saveData = CurrentData;
            // If no save data is available (e.g., main menu), display a warning.
            if (saveData == null)
            {
                Widgets.Label(inRect, "AWA_LoadSaveFirst".Translate());
                return;
            }

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            try
            {
                // --- Basic Toggles ---
                listingStandard.CheckboxLabeled(
                    "AWA_EnableModLabel".Translate(), 
                    ref saveData.modEnabled, 
                    "AWA_EnableModTooltip".Translate()
                );
                
                listingStandard.CheckboxLabeled(
                    "AWA_EnableDailyRefreshLabel".Translate(), 
                    ref saveData.enableDailyRefresh, 
                    "AWA_EnableDailyRefreshTooltip".Translate()
                );

                listingStandard.GapLine(12f);

                // --- Assignment Mode Selector ---
                listingStandard.Label("Assignment System Mode:");
                
                Rect modeRect = listingStandard.GetRect(30f);
                float radioSize = 24f;
                float gapBetweenRadioAndLabel = 5f;
                float gapBetweenModes = 20f;
                
                // Calculate actual text widths for proper alignment
                GameFont previousFont = Text.Font;
                Text.Font = GameFont.Small;
                float simpleWidth = Text.CalcSize("Simple Mode").x;
                float expertWidth = Text.CalcSize("Expert Mode").x;
                float hybridWidth = Text.CalcSize("Hybrid Mode").x;
                Text.Font = previousFont;
                
                float currentX = modeRect.x;
                
                // Option 1: Simple Mode
                Rect simpleRadioRect = new Rect(currentX, modeRect.y + 3f, radioSize, radioSize);
                Rect simpleLabelRect = new Rect(simpleRadioRect.xMax + gapBetweenRadioAndLabel, modeRect.y, simpleWidth, modeRect.height);
                bool isSimple = saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Simple;
                if (Widgets.RadioButton(simpleRadioRect.position, isSimple))
                {
                    saveData.assignmentMode = AutomatedWork_SaveData.AssignmentMode.Simple;
                }
                Widgets.Label(simpleLabelRect, "Simple Mode");
                Rect simpleTooltipRect = new Rect(simpleRadioRect.x, simpleRadioRect.y, simpleLabelRect.xMax - simpleRadioRect.x, modeRect.height);
                TooltipHandler.TipRegion(simpleTooltipRect, "Uses only the Count/Priority sliders below. Expert Mode rules are ignored.");
                
                currentX = simpleLabelRect.xMax + gapBetweenModes;
                
                // Option 2: Expert Mode
                Rect expertRadioRect = new Rect(currentX, modeRect.y + 3f, radioSize, radioSize);
                Rect expertLabelRect = new Rect(expertRadioRect.xMax + gapBetweenRadioAndLabel, modeRect.y, expertWidth, modeRect.height);
                bool isExpert = saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Expert;
                if (Widgets.RadioButton(expertRadioRect.position, isExpert))
                {
                    saveData.assignmentMode = AutomatedWork_SaveData.AssignmentMode.Expert;
                }
                Widgets.Label(expertLabelRect, "Expert Mode");
                Rect expertTooltipRect = new Rect(expertRadioRect.x, expertRadioRect.y, expertLabelRect.xMax - expertRadioRect.x, modeRect.height);
                TooltipHandler.TipRegion(expertTooltipRect, "Uses ONLY skill-based rules from Expert Mode. Simple sliders are ignored (except Count/Percentage).");
                
                currentX = expertLabelRect.xMax + gapBetweenModes;
                
                // Option 3: Hybrid Mode
                Rect hybridRadioRect = new Rect(currentX, modeRect.y + 3f, radioSize, radioSize);
                Rect hybridLabelRect = new Rect(hybridRadioRect.xMax + gapBetweenRadioAndLabel, modeRect.y, hybridWidth, modeRect.height);
                bool isHybrid = saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Hybrid;
                if (Widgets.RadioButton(hybridRadioRect.position, isHybrid))
                {
                    saveData.assignmentMode = AutomatedWork_SaveData.AssignmentMode.Hybrid;
                }
                Widgets.Label(hybridLabelRect, "Hybrid Mode");
                Rect hybridTooltipRect = new Rect(hybridRadioRect.x, hybridRadioRect.y, hybridLabelRect.xMax - hybridRadioRect.x, modeRect.height);
                TooltipHandler.TipRegion(hybridTooltipRect, "Expert rules override when they match. Simple Mode is fallback for unmatched skills.");

                listingStandard.Gap(6f);

                // --- Advanced Toggles ---
                listingStandard.CheckboxLabeled(
                    "Force Emergency Priorities (Doctor/Firefighter = P1)",
                    ref saveData.forceEmergencyPriorities,
                    "When enabled, Doctor and Firefighter are always forced to priority 1, overriding all other settings."
                );

                if (saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Expert)
                {
                    listingStandard.CheckboxLabeled(
                        "Prioritize Passion in Expert Mode",
                        ref saveData.prioritizePassionInExpertMode,
                        "When enabled, pawns are sorted by passion FIRST, then skill. Useful for training passionate colonists."
                    );
                }

                listingStandard.GapLine(12f);

                // --- Management Buttons ---
                Rect buttonRowRect = listingStandard.GetRect(30f); 
                float buttonWidth = (buttonRowRect.width - 10f) / 2f;
                
                Rect exclusionsButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, buttonRowRect.height);
                if (Widgets.ButtonText(exclusionsButtonRect, "AWA_ManageExclusionsButton".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_ManageExclusions(saveData));
                }
                TooltipHandler.TipRegion(exclusionsButtonRect, "Exclude specific colonists from ALL automatic assignments.");

                Rect expertModeButtonRect = new Rect(exclusionsButtonRect.xMax + 10f, buttonRowRect.y, buttonWidth, buttonRowRect.height);
                if (Widgets.ButtonText(expertModeButtonRect, "AWA_ConfigureExpertModeButton".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_ExpertModeSettings());
                }
                TooltipHandler.TipRegion(expertModeButtonRect, "Configure skill-based priority rules (Expert Mode).");
        
                listingStandard.Gap(12f);
                listingStandard.GapLine(12f);
        
                listingStandard.Label("AWA_DesiredPawnsLabel".Translate());
            }
            catch (Exception ex) 
            { 
                Log.Error($"[AutoWork] Exception drawing general settings: {ex}"); 
            }
            
            // Determine the maximum number of colonists for slider limits
            int maxPawnCountForSlider = 10;
            try
            {
                int eligibleCount = WorkAssigner.GetEligibleColonistCount(saveData);
                maxPawnCountForSlider = Mathf.Max(0, eligibleCount);
            }
            catch (Exception ex) 
            { 
                Log.Error($"[AutoWork] Exception getting eligible count: {ex}"); 
            }

            // --- Scrollable Work Type List ---
            // Fix: Increased row height to prevent overlapping elements.
            const float rowHeight = 95f; 

            Rect outRect = default;
            Rect viewRect = default;
            List<WorkTypeDef> relevantWorkTypes = null;

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
                listingStandard.End(); 
                return;
            }

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
                        
                        // Optional: Draw a subtle highlight on alternate rows for better readability
                        if (relevantWorkTypes.IndexOf(workDef) % 2 == 1)
                        {
                            Widgets.DrawLightHighlight(rowRect);
                        }

                        float currentX = rowRect.x;
                        float controlAreaHeight = 28f; // Slightly reduced for tighter packing
                        float rowCenterY = rowRect.y + 6f; // Top padding

                        // --- Row 1: Label, Checkbox, Mode Toggle, Main Sliders ---

                        // Label
                        float labelWidth = viewRect.width * 0.20f;
                        Rect labelRect = new Rect(currentX + 5f, rowCenterY, labelWidth, controlAreaHeight);
                        Widgets.Label(labelRect, workDef.labelShort.CapitalizeFirst());
                        currentX += labelWidth + 5f;

                        // Include Checkbox
                        float checkboxSize = Widgets.CheckboxSize;
                        Rect includeCheckboxRect = new Rect(currentX, rowCenterY + (controlAreaHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);
                        
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
                                if (saveData.excludedWorkTypeDefNames == null) 
                                    saveData.excludedWorkTypeDefNames = new List<string>();
                                if (!saveData.excludedWorkTypeDefNames.Contains(defName)) 
                                    saveData.excludedWorkTypeDefNames.Add(defName);
                            }
                        }
                        currentX += checkboxSize + 10f;

                        // Count/Percentage Mode Toggle
                        float toggleWidth = 70f;
                        Rect toggleRect = new Rect(currentX, rowCenterY, toggleWidth, controlAreaHeight);
                        string toggleLabel = currentSetting.usePercentage ? "Mode: %" : "Mode: #";
                        if(Widgets.ButtonText(toggleRect, toggleLabel))
                        {
                            currentSetting.usePercentage = !currentSetting.usePercentage;
                        }
                        TooltipHandler.TipRegion(toggleRect, "AWA_ToggleCountModeTooltip".Translate());
                        currentX += toggleWidth + 10f;

                        // Calculate width for sliders (shared by both rows)
                        float remainingWidth = viewRect.width - currentX - 10f;
                        float sliderWidth = (remainingWidth / 2f) - 5f;
                        
                        // First Row Sliders: Count/Percentage & Priority
                        Rect countPercentGroupRect = new Rect(currentX, rowCenterY, sliderWidth, controlAreaHeight);
                        if (currentSetting.usePercentage)
                        {
                            currentSetting.percentage = Widgets.HorizontalSlider(
                                countPercentGroupRect,
                                currentSetting.percentage,
                                0f, 1f,
                                true,
                                "AWA_PercentageLabel".Translate(currentSetting.percentage.ToStringPercent()),
                                roundTo: 0.01f
                            );
                        }
                        else
                        {
                            float tempCount = Mathf.Clamp(currentSetting.count, 0, maxPawnCountForSlider);
                            tempCount = Widgets.HorizontalSlider(
                                countPercentGroupRect,
                                tempCount,
                                0f, (float)maxPawnCountForSlider,
                                true,
                                "AWA_FixedCountLabel".Translate(currentSetting.count),
                                roundTo: 1f
                            );
                            if((int)tempCount != currentSetting.count) 
                            { 
                                currentSetting.count = (int)tempCount; 
                            }
                        }

                        Rect priorityGroupRect = new Rect(currentX + sliderWidth + 5f, rowCenterY, sliderWidth, controlAreaHeight);
                        float tempPriority = Mathf.Clamp(currentSetting.priority, 1, 4);
                        tempPriority = Widgets.HorizontalSlider(
                            priorityGroupRect,
                            tempPriority,
                            1f, 4f,
                            true,
                            "AWA_PriorityFieldLabel".Translate(currentSetting.priority),
                            roundTo: 1f
                        );
                        if((int)tempPriority != currentSetting.priority) 
                        { 
                            currentSetting.priority = (int)tempPriority; 
                        }

                        // --- Row 2: Passion Weight & Fallback Priority ---
                        // Fix: Proper vertical offset and alignment with sliders above
                        
                        float secondRowY = rowCenterY + controlAreaHeight + 4f; 
                        
                        // Align start of second row with the sliders of the first row
                        float secondRowStartX = currentX; 
                        
                        // Passion Slider
                        Rect passionWeightRect = new Rect(secondRowStartX, secondRowY, sliderWidth, 22f);
                        string passionLabel = $"Passion: {currentSetting.passionWeight:F1}x";
                        float newPassionWeight = Widgets.HorizontalSlider(
                            passionWeightRect,
                            currentSetting.passionWeight,
                            0f, 3f,
                            true,
                            passionLabel,
                            "0x", "3x",
                            roundTo: 0.1f
                        );
                        if (Math.Abs(newPassionWeight - currentSetting.passionWeight) > 0.01f)
                        {
                            currentSetting.passionWeight = newPassionWeight;
                        }
                        TooltipHandler.TipRegion(passionWeightRect, 
                            "How much passion affects assignment priority.\n" +
                            "0x = Ignore passion entirely\n" +
                            "1x = Default balance\n" +
                            "3x = Strongly prefer passionate pawns");

                        // Fallback Priority Slider
                        Rect fallbackRect = new Rect(secondRowStartX + sliderWidth + 5f, secondRowY, sliderWidth, 22f);
                        string fallbackLabel = $"Backup: {(currentSetting.fallbackPriority == 0 ? "OFF" : "P" + currentSetting.fallbackPriority)}";
                        float tempFallback = Widgets.HorizontalSlider(
                            fallbackRect,
                            currentSetting.fallbackPriority,
                            0f, 4f,
                            true,
                            fallbackLabel,
                            "OFF", "P4",
                            roundTo: 1f
                        );
                        if((int)tempFallback != currentSetting.fallbackPriority)
                        {
                            currentSetting.fallbackPriority = (int)tempFallback;
                        }
                        TooltipHandler.TipRegion(fallbackRect,
                            "Priority for colonists NOT in top selection.\n" +
                            "0 = Disable work\n" +
                            "1-4 = Backup priority (useful for Hauling/Cleaning)");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AutoWork] Exception drawing row for '{workDef?.defName ?? "NULL"}': {ex}");
                        Rect errorRect = new Rect(viewRect.x, currentY, viewRect.width, 30f);
                        Widgets.Label(errorRect, $"Error processing {workDef?.labelShort ?? "Unknown"}");
                    }
                    finally
                    {
                        currentY += rowHeight;
                    }
                }
            }

            Widgets.EndScrollView();
            listingStandard.End();
        }
    }
}