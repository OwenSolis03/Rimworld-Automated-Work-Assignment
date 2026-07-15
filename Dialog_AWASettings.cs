using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Standalone settings window for Automated Work Assignment.
    /// Provides a draggable, resizable window containing all mod configuration options.
    /// Window position and size persist across open/close within a session.
    /// </summary>
    public class Dialog_AWASettings : Window
    {
        /// <summary>
        /// Current scroll position for the work type settings list.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Cached list of work types relevant to the mod (excluding work types with no work tags).
        /// </summary>
        private static List<WorkTypeDef> cachedRelevantWorkTypes = null;

        /// <summary>
        /// The default initial size for the settings window.
        /// </summary>
        private static readonly Vector2 DefaultSize = new Vector2(900f, 700f);

        /// <summary>
        /// Minimum window size to keep the UI usable.
        /// </summary>
        private static readonly Vector2 MinSize = new Vector2(700f, 450f);

        // --- Position/Size Persistence ---
        private static Rect? savedWindowRect = null;

        // --- Collapsible Section States (static so they persist across open/close) ---
        private static bool collapseBasicSettings = false;
        private static bool collapseManagement = false;
        private static bool collapseSimplification = false;
        private static bool collapseWorkTypes = false;
        private static Dictionary<string, bool> workTypeCollapsed = new Dictionary<string, bool>();

        public override Vector2 InitialSize => DefaultSize;

        /// <summary>
        /// Initializes the window with RimWorld-standard properties.
        /// </summary>
        public Dialog_AWASettings()
        {
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            draggable = true;
            resizeable = true;
        }

        /// <summary>
        /// Restores saved window position/size on open.
        /// </summary>
        public override void PostOpen()
        {
            base.PostOpen();
            if (savedWindowRect.HasValue)
            {
                windowRect = savedWindowRect.Value;
            }
        }

        /// <summary>
        /// Saves window position/size on close.
        /// </summary>
        public override void PostClose()
        {
            savedWindowRect = windowRect;
            base.PostClose();
        }

        /// <summary>
        /// Draws a clickable section header with a collapse/expand triangle indicator.
        /// Returns true if the section is expanded (content should be drawn).
        /// </summary>
        private bool DrawSectionHeader(Listing_Standard listing, string label, ref bool collapsed)
        {
            Rect headerRect = listing.GetRect(24f);
            
            // Draw highlight on hover
            Widgets.DrawHighlightIfMouseover(headerRect);

            // Draw triangle indicator
            string arrow = collapsed ? "▶ " : "▼ ";
            
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            Widgets.Label(headerRect, arrow + label);
            Text.Font = prevFont;

            // Handle click
            if (Widgets.ButtonInvisible(headerRect))
            {
                collapsed = !collapsed;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            return !collapsed;
        }

        /// <summary>
        /// Draws the full settings UI inside the window.
        /// </summary>
        /// <param name="inRect">The rectangular area available for drawing.</param>
        public override void DoWindowContents(Rect inRect)
        {
            // Enforce minimum window size
            if (windowRect.width < MinSize.x) windowRect.width = MinSize.x;
            if (windowRect.height < MinSize.y) windowRect.height = MinSize.y;

            var saveData = AutomatedWorkAssignmentMod.CurrentData;
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
                // ============================================================
                // SECTION: Basic Settings (collapsible)
                // ============================================================
                if (DrawSectionHeader(listingStandard, "Basic Settings", ref collapseBasicSettings))
                {
                    listingStandard.Gap(4f);

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

                    listingStandard.Gap(6f);

                    // --- Assignment Mode Selector ---
                    listingStandard.Label("Assignment System Mode:");
                    
                    Rect modeRect = listingStandard.GetRect(30f);
                    float radioSize = 24f;
                    float gapBetweenRadioAndLabel = 5f;
                    float gapBetweenModes = 20f;
                    
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
                    Widgets.Label(simpleLabelRect, "AWA_AssignmentMode_Simple".Translate());
                    Rect simpleTooltipRect = new Rect(simpleRadioRect.x, simpleRadioRect.y, simpleLabelRect.xMax - simpleRadioRect.x, modeRect.height);
                    TooltipHandler.TipRegion(simpleTooltipRect, "AWA_AssignmentMode_Simple_Tooltip".Translate());
                    
                    currentX = simpleLabelRect.xMax + gapBetweenModes;
                    
                    // Option 2: Expert Mode
                    Rect expertRadioRect = new Rect(currentX, modeRect.y + 3f, radioSize, radioSize);
                    Rect expertLabelRect = new Rect(expertRadioRect.xMax + gapBetweenRadioAndLabel, modeRect.y, expertWidth, modeRect.height);
                    bool isExpert = saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Expert;
                    if (Widgets.RadioButton(expertRadioRect.position, isExpert))
                    {
                        saveData.assignmentMode = AutomatedWork_SaveData.AssignmentMode.Expert;
                    }
                    Widgets.Label(expertLabelRect, "AWA_AssignmentMode_Expert".Translate());
                    Rect expertTooltipRect = new Rect(expertRadioRect.x, expertRadioRect.y, expertLabelRect.xMax - expertRadioRect.x, modeRect.height);
                    TooltipHandler.TipRegion(expertTooltipRect, "AWA_AssignmentMode_Expert_Tooltip".Translate());
                    
                    currentX = expertLabelRect.xMax + gapBetweenModes;
                    
                    // Option 3: Hybrid Mode
                    Rect hybridRadioRect = new Rect(currentX, modeRect.y + 3f, radioSize, radioSize);
                    Rect hybridLabelRect = new Rect(hybridRadioRect.xMax + gapBetweenRadioAndLabel, modeRect.y, hybridWidth, modeRect.height);
                    bool isHybrid = saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Hybrid;
                    if (Widgets.RadioButton(hybridRadioRect.position, isHybrid))
                    {
                        saveData.assignmentMode = AutomatedWork_SaveData.AssignmentMode.Hybrid;
                    }
                    Widgets.Label(hybridLabelRect, "AWA_AssignmentMode_Hybrid".Translate());
                    Rect hybridTooltipRect = new Rect(hybridRadioRect.x, hybridRadioRect.y, hybridLabelRect.xMax - hybridRadioRect.x, modeRect.height);
                    TooltipHandler.TipRegion(hybridTooltipRect, "AWA_AssignmentMode_Hybrid_Tooltip".Translate());

                    listingStandard.Gap(6f);

                    // --- Advanced Toggles ---
                    listingStandard.CheckboxLabeled(
                        "AWA_ForceEmergencyPriorities".Translate(),
                        ref saveData.forceEmergencyPriorities,
                        "AWA_ForceEmergencyPriorities_Tooltip".Translate()
                    );

                    if (saveData.assignmentMode == AutomatedWork_SaveData.AssignmentMode.Expert)
                    {
                        listingStandard.CheckboxLabeled(
                            "AWA_PrioritizePassion".Translate(),
                            ref saveData.prioritizePassionInExpertMode,
                            "AWA_PrioritizePassion_Tooltip".Translate()
                        );
                    }
                }

                listingStandard.GapLine(8f);

                // ============================================================
                // SECTION: Management Buttons (collapsible)
                // ============================================================
                if (DrawSectionHeader(listingStandard, "Management Tools", ref collapseManagement))
                {
                    listingStandard.Gap(4f);
                    Rect buttonRowRect = listingStandard.GetRect(30f); 
                    float buttonWidth = (buttonRowRect.width - 20f) / 3f;
                    
                    Rect exclusionsButtonRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, buttonRowRect.height);
                    if (Widgets.ButtonText(exclusionsButtonRect, "AWA_ManageExclusionsButton".Translate()))
                    {
                        Find.WindowStack.Add(new Dialog_ManageExclusions(saveData));
                    }
                    TooltipHandler.TipRegion(exclusionsButtonRect, "AWA_ExcludePawns_Tooltip".Translate());

                    Rect expertModeButtonRect = new Rect(exclusionsButtonRect.xMax + 10f, buttonRowRect.y, buttonWidth, buttonRowRect.height);
                    if (Widgets.ButtonText(expertModeButtonRect, "AWA_ConfigureExpertModeButton".Translate()))
                    {
                        Find.WindowStack.Add(new Dialog_ExpertModeSettings());
                    }
                    TooltipHandler.TipRegion(expertModeButtonRect, "AWA_ConfigureExpertModeTooltip".Translate());

                    Rect heuristicsButtonRect = new Rect(expertModeButtonRect.xMax + 10f, buttonRowRect.y, buttonWidth, buttonRowRect.height);
                    if (Widgets.ButtonText(heuristicsButtonRect, "Experimental"))
                    {
                        Find.WindowStack.Add(new Experimental.UI.Dialog_ExperimentalSettings(saveData));
                    }
                    TooltipHandler.TipRegion(heuristicsButtonRect, "Configure experimental heuristic profiling.");
                }

                listingStandard.GapLine(8f);

                // ============================================================
                // SECTION: UI Simplification (collapsible)
                // ============================================================
                if (DrawSectionHeader(listingStandard, "UI Simplification (Optional)", ref collapseSimplification))
                {
                    listingStandard.Gap(4f);
                    listingStandard.CheckboxLabeled(
                        "Enable Master Passion Slider",
                        ref saveData.useMasterPassion,
                        "Replaces individual passion sliders with a single global slider."
                    );
                    if (saveData.useMasterPassion)
                    {
                        saveData.masterPassionWeight = Widgets.HorizontalSlider(
                            listingStandard.GetRect(22f),
                            saveData.masterPassionWeight, 0f, 3f, true,
                            "Master Passion Weight: " + saveData.masterPassionWeight.ToString("F1"),
                            "0x", "3x", roundTo: 0.1f
                        );
                    }
                    listingStandard.CheckboxLabeled(
                        "Combine Similar Work Types",
                        ref saveData.combineSimilarWorkTypes,
                        "Visually merges related work types (e.g. Grow/Plant Cut) into single rows."
                    );
                    listingStandard.CheckboxLabeled(
                        "Gradual Backup Scaling",
                        ref saveData.gradualBackupScaling,
                        "Currently hides Backup Priority sliders. (Graded banding logic coming later)"
                    );
                }

                listingStandard.GapLine(8f);

                // ============================================================
                // SECTION: Work Types (collapsible)
                // ============================================================
                if (!DrawSectionHeader(listingStandard, "Work Type Settings", ref collapseWorkTypes))
                {
                    // Section collapsed — skip work type list rendering entirely
                    listingStandard.End();
                    return;
                }
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
            const float rowHeight = 95f; 
            const float collapsedRowHeight = 35f;

            Rect outRect = default;
            Rect viewRect = default;
            List<WorkTypeDef> relevantWorkTypes = null;
            List<List<WorkTypeDef>> groupedWorkTypes = new List<List<WorkTypeDef>>();

            try
            {
                float currentYPos = listingStandard.CurHeight;
                float availableHeight = inRect.height - currentYPos - 10f;
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

                if (saveData.combineSimilarWorkTypes)
                {
                    // Note: Visual consolidation only. Save data still writes per underlying WorkTypeDef.
                    // Full data model consolidation would require refactoring ExposeData and WorkAssigner logic.
                    var plantsGroup = relevantWorkTypes.Where(w => w.defName == "Growing" || w.defName == "PlantCutting").ToList();
                    var craftingGroup = relevantWorkTypes.Where(w => w.defName == "Smithing" || w.defName == "Tailoring" || w.defName == "Crafting").ToList();
                    
                    HashSet<WorkTypeDef> grouped = new HashSet<WorkTypeDef>(plantsGroup.Concat(craftingGroup));
                    foreach (var wt in relevantWorkTypes)
                    {
                        if (!grouped.Contains(wt))
                        {
                            groupedWorkTypes.Add(new List<WorkTypeDef> { wt });
                        }
                        else if (plantsGroup.Count > 0 && plantsGroup[0] == wt)
                        {
                            groupedWorkTypes.Add(plantsGroup);
                        }
                        else if (craftingGroup.Count > 0 && craftingGroup[0] == wt)
                        {
                            groupedWorkTypes.Add(craftingGroup);
                        }
                    }
                }
                else
                {
                    groupedWorkTypes = relevantWorkTypes.Select(w => new List<WorkTypeDef> { w }).ToList();
                }

                float totalContentHeight = 0f;
                foreach (var group in groupedWorkTypes)
                {
                    if (group == null || group.Count == 0) continue;
                    string defName = group[0].defName;
                    if (!workTypeCollapsed.ContainsKey(defName)) workTypeCollapsed[defName] = false; // default expanded
                    totalContentHeight += workTypeCollapsed[defName] ? collapsedRowHeight : rowHeight;
                }

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

            if (groupedWorkTypes != null)
            {
                for (int i = 0; i < groupedWorkTypes.Count; i++)
                {
                    var group = groupedWorkTypes[i];
                    try 
                    {
                        if (group == null || group.Count == 0) continue;
                        WorkTypeDef workDef = group[0];
                        if (workDef == null) continue;
                        string defName = workDef.defName;
                        WorkSettingValues currentSetting = saveData.GetWorkSetting(defName);
                        if (currentSetting == null) continue;

                        if (!workTypeCollapsed.ContainsKey(defName)) workTypeCollapsed[defName] = false;
                        bool isCollapsed = workTypeCollapsed[defName];
                        float currentRowHeight = isCollapsed ? collapsedRowHeight : rowHeight;

                        Rect rowRect = new Rect(viewRect.x, currentY, viewRect.width, currentRowHeight);
                        
                        // Optional: Draw a subtle highlight on alternate rows for better readability
                        if (i % 2 == 1)
                        {
                            Widgets.DrawLightHighlight(rowRect);
                        }

                        float rowCurrentX = rowRect.x;
                        float controlAreaHeight = 28f;
                        float rowCenterY = rowRect.y + 6f;

                        // --- Row 1: Label, Checkbox, Mode Toggle, Main Sliders ---

                        // Label
                        float labelWidth = viewRect.width * 0.20f;
                        Rect labelRect = new Rect(rowCurrentX + 5f, rowCenterY, labelWidth, controlAreaHeight);
                        string displayLabel = workDef.labelShort.CapitalizeFirst();
                        if (saveData.combineSimilarWorkTypes && group.Count > 1)
                        {
                            if (defName == "Growing" || defName == "PlantCutting") displayLabel = "Plants (Combined)";
                            else if (defName == "Smithing" || defName == "Tailoring" || defName == "Crafting") displayLabel = "Crafting (Combined)";
                        }
                        
                        string arrow = isCollapsed ? "▶ " : "▼ ";
                        Widgets.DrawHighlightIfMouseover(labelRect);
                        Widgets.Label(labelRect, arrow + displayLabel);
                        if (Widgets.ButtonInvisible(labelRect))
                        {
                            workTypeCollapsed[defName] = !isCollapsed;
                            SoundDefOf.Click.PlayOneShotOnCamera();
                        }
                        rowCurrentX += labelWidth + 5f;

                        // Include Checkbox
                        float checkboxSize = Widgets.CheckboxSize;
                        Rect includeCheckboxRect = new Rect(rowCurrentX, rowCenterY + (controlAreaHeight - checkboxSize) / 2f, checkboxSize, checkboxSize);
                        
                        bool isIncluded = !(saveData.excludedWorkTypeDefNames?.Contains(defName) ?? false);
                        bool checkboxState = isIncluded;
                        Widgets.Checkbox(includeCheckboxRect.position, ref checkboxState, checkboxSize);
                        TooltipHandler.TipRegion(includeCheckboxRect, "AWA_IncludeTaskTooltip".Translate());

                        if (checkboxState != isIncluded)
                        {
                            foreach (var wt in group)
                            {
                                if (checkboxState) 
                                {
                                    saveData.excludedWorkTypeDefNames?.Remove(wt.defName);
                                }
                                else
                                {
                                    if (saveData.excludedWorkTypeDefNames == null) 
                                        saveData.excludedWorkTypeDefNames = new List<string>();
                                    if (!saveData.excludedWorkTypeDefNames.Contains(wt.defName)) 
                                        saveData.excludedWorkTypeDefNames.Add(wt.defName);
                                }
                            }
                        }
                        rowCurrentX += checkboxSize + 10f;

                        if (!isCollapsed)
                        {
                            // Count/Percentage Mode Toggle
                            float toggleWidth = 70f;
                            Rect toggleRect = new Rect(rowCurrentX, rowCenterY, toggleWidth, controlAreaHeight);
                            string toggleLabel = currentSetting.usePercentage ? "AWA_ModeToggle_Percentage".Translate() : "AWA_ModeToggle_Count".Translate();
                            if(Widgets.ButtonText(toggleRect, toggleLabel))
                            {
                                bool newVal = !currentSetting.usePercentage;
                                foreach (var wt in group) saveData.GetWorkSetting(wt.defName).usePercentage = newVal;
                            }
                            TooltipHandler.TipRegion(toggleRect, "AWA_ToggleCountModeTooltip".Translate());
                            rowCurrentX += toggleWidth + 10f;

                            // Calculate width for sliders (shared by both rows)
                            float remainingWidth = viewRect.width - rowCurrentX - 10f;
                            float sliderWidth = (remainingWidth / 2f) - 5f;
                            
                            // First Row Sliders: Count/Percentage & Priority
                            Rect countPercentGroupRect = new Rect(rowCurrentX, rowCenterY, sliderWidth, controlAreaHeight);
                            if (currentSetting.usePercentage)
                            {
                                float newPct = Widgets.HorizontalSlider(
                                    countPercentGroupRect,
                                    currentSetting.percentage,
                                    0f, 1f,
                                    true,
                                    "AWA_PercentageLabel".Translate(currentSetting.percentage.ToStringPercent()),
                                    roundTo: 0.01f
                                );
                                if (Mathf.Abs(newPct - currentSetting.percentage) > 0.001f)
                                {
                                    foreach (var wt in group) saveData.GetWorkSetting(wt.defName).percentage = newPct;
                                }
                            }
                            else
                            {
                                float tempCount = Mathf.Clamp(currentSetting.count, 0, maxPawnCountForSlider);
                                float newCount = Widgets.HorizontalSlider(
                                    countPercentGroupRect,
                                    tempCount,
                                    0f, (float)maxPawnCountForSlider,
                                    true,
                                    "AWA_FixedCountLabel".Translate(currentSetting.count),
                                    roundTo: 1f
                                );
                                if((int)newCount != currentSetting.count) 
                                { 
                                    foreach (var wt in group) saveData.GetWorkSetting(wt.defName).count = (int)newCount;
                                }
                            }

                            Rect priorityGroupRect = new Rect(rowCurrentX + sliderWidth + 5f, rowCenterY, sliderWidth, controlAreaHeight);
                            float tempPriority = Mathf.Clamp(currentSetting.priority, 0, 4);
                            string priorityLabelArg = currentSetting.priority == 0 ? "AWA_Off".Translate().ToString() : currentSetting.priority.ToString();
                            float newPriority = Widgets.HorizontalSlider(
                                priorityGroupRect,
                                tempPriority,
                                0f, 4f,
                                true,
                                "AWA_PriorityFieldLabel".Translate(priorityLabelArg),
                                roundTo: 1f
                            );
                            if((int)newPriority != currentSetting.priority) 
                            { 
                                foreach (var wt in group) saveData.GetWorkSetting(wt.defName).priority = (int)newPriority;
                            }

                            // --- Row 2: Passion Weight & Fallback Priority ---
                            float secondRowY = rowCenterY + controlAreaHeight + 4f; 
                            float secondRowStartX = rowCurrentX; 
                            
                            // Passion Slider
                            if (saveData.useMasterPassion)
                            {
                                foreach (var wt in group) saveData.GetWorkSetting(wt.defName).passionWeight = saveData.masterPassionWeight;
                            }
                            else
                            {
                                Rect passionWeightRect = new Rect(secondRowStartX, secondRowY, sliderWidth, 22f);
                                string passionLabel = "AWA_PassionWeightLabel".Translate(currentSetting.passionWeight.ToString("F1"));
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
                                    foreach (var wt in group) saveData.GetWorkSetting(wt.defName).passionWeight = newPassionWeight;
                                }
                                TooltipHandler.TipRegion(passionWeightRect, "AWA_PassionWeight_Tooltip".Translate());
                            }

                            // Fallback Priority Slider
                            if (saveData.gradualBackupScaling)
                            {
                                // TODO: graded banding logic
                            }
                            else
                            {
                                Rect fallbackRect = new Rect(secondRowStartX + sliderWidth + 5f, secondRowY, sliderWidth, 22f);
                                string fallbackLabel = "AWA_BackupPriorityLabel".Translate(currentSetting.fallbackPriority == 0 ? "AWA_Off".Translate() : ("P" + currentSetting.fallbackPriority));
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
                                    foreach (var wt in group) saveData.GetWorkSetting(wt.defName).fallbackPriority = (int)tempFallback;
                                }
                                TooltipHandler.TipRegion(fallbackRect, "AWA_BackupPriority_Tooltip".Translate());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AutoWork] Exception drawing row for '{group?[0]?.defName ?? "NULL"}': {ex}");
                        Rect errorRect = new Rect(viewRect.x, currentY, viewRect.width, 30f);
                        Widgets.Label(errorRect, $"Error processing {group?[0]?.labelShort ?? "Unknown"}");
                    }
                    finally
                    {
                        string defNameForHeight = group?[0]?.defName;
                        bool wasCollapsed = false;
                        if (defNameForHeight != null && workTypeCollapsed.ContainsKey(defNameForHeight))
                        {
                            wasCollapsed = workTypeCollapsed[defNameForHeight];
                        }
                        currentY += wasCollapsed ? collapsedRowHeight : rowHeight;
                    }
                }
            }

            Widgets.EndScrollView();
            listingStandard.End();
        }
    }
}
