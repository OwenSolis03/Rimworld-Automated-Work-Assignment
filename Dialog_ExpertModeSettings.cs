using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Provides a dialog window for configuring skill-based priority rules (Expert Mode)
    /// for different work types within the Automated Work Assignment mod system.
    /// This window allows users to create granular rules (e.g., "If Cooking skill is 10-20, set priority to 2")
    /// and manage per-job pawn exclusions.
    /// </summary>
    public class Dialog_ExpertModeSettings : Window
    {
        private ExpertModeRuleManager ruleManager;
        private Vector2 scrollPositionLeft = Vector2.zero;
        private Vector2 scrollPositionRight = Vector2.zero;
        private WorkTypeDef selectedWorkDef = null;
        private List<WorkTypeDef> relevantWorkTypesCache = new List<WorkTypeDef>();

        // --- Position/Size Persistence ---
        private static Rect? savedWindowRect = null;

        /// <summary>
        /// Defines the initial dimensions of the window.
        /// </summary>
        public override Vector2 InitialSize => new Vector2(800f, 600f);

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialog_ExpertModeSettings"/> class.
        /// Sets up window properties (pause, close on click outside) and caches relevant work types
        /// to optimize rendering performance.
        /// </summary>
        public Dialog_ExpertModeSettings()
        {
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;

            ruleManager = Current.Game?.GetComponent<ExpertModeRuleManager>();

            // Cache work types that have work tags (exclude hidden/internal types)
            relevantWorkTypesCache = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                .OrderBy(wtd => wtd.labelShort)
                .ToList();

            // Dynamic title showing skill cap if extended skills (e.g., Vanilla Skills Expanded) are detected
            int maxCap = SkillPriorityRule.MaxSkillLevel;
            if (maxCap > 20)
            {
                this.optionalTitle = $"AWA_ExpertMode_RuleWindowTitle".Translate() + $" (Skills up to {maxCap})";
            }
            else
            {
                this.optionalTitle = "AWA_ExpertMode_RuleWindowTitle".Translate();
            }
        }

        /// <summary>
        /// Called before the window opens.
        /// Automatically selects the first valid work type to avoid an empty selection state in the UI.
        /// Prioritizes work types that actually have relevant skills associated with them.
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen();
            if (selectedWorkDef == null && relevantWorkTypesCache.Any())
            {
                // Pre-select first work type with associated skill (skip skillless jobs if possible)
                selectedWorkDef = relevantWorkTypesCache
                                      .FirstOrDefault(w => w.relevantSkills != null && w.relevantSkills.Any())
                                  ?? relevantWorkTypesCache.First();
            }
            else if (selectedWorkDef != null && !relevantWorkTypesCache.Contains(selectedWorkDef))
            {
                // If the previously selected def is no longer valid, reset to the first available
                selectedWorkDef = relevantWorkTypesCache.FirstOrDefault();
            }
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
        /// Draws the main content of the window.
        /// Splits the view into a left sidebar (work type list) and a right content area (rule editor).
        /// </summary>
        /// <param name="inRect">The available drawing area.</param>
        public override void DoWindowContents(Rect inRect)
        {
            if (ruleManager == null)
            {
                Rect errorRect = new Rect(inRect.x + 10f, inRect.y + 10f, inRect.width - 20f, 30f);
                Widgets.Label(errorRect, "AWA_ExpertMode_LoadSaveFirst".Translate());
                return;
            }

            float footerHeight = CloseButSize.y + 10f;
            float contentHeight = inRect.height - footerHeight;
            
            // Define layout areas
            Rect leftRect = new Rect(inRect.x, inRect.y, inRect.width * 0.3f, contentHeight);
            Rect rightRect = new Rect(leftRect.xMax + 10f, inRect.y, inRect.width - leftRect.width - 20f, contentHeight);

            DrawWorkTypeDefList(leftRect);

            if (selectedWorkDef != null)
            {
                DrawRuleEditor(rightRect, selectedWorkDef);
            }
            else
            {
                Widgets.Label(rightRect.ContractedBy(10f), "AWA_ExpertMode_SelectWorkTypePrompt".Translate());
            }

            // Draw Close button
            Rect closeButtonRect = new Rect(inRect.width - CloseButSize.x - 10f, inRect.height - CloseButSize.y - 5f, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
            {
                this.Close();
            }
        }

        /// <summary>
        /// Draws the scrollable list of work types on the left side.
        /// Provides visual feedback for selection and indicates work types without associated skills.
        /// </summary>
        /// <param name="rect">The area allocated for the list.</param>
        private void DrawWorkTypeDefList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            if (!relevantWorkTypesCache.Any())
            {
                Widgets.Label(rect.ContractedBy(10f), "No relevant work types found.");
                return;
            }

            float entryHeight = 30f;
            float viewHeight = relevantWorkTypesCache.Count * entryHeight;
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);

            Widgets.BeginScrollView(rect, ref scrollPositionLeft, viewRect, true);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (WorkTypeDef workDef in relevantWorkTypesCache)
            {
                if (workDef == null) continue;

                bool isSelected = selectedWorkDef == workDef;
                bool hasSkill = workDef.relevantSkills != null && workDef.relevantSkills.Any();
                
                Rect entryRect = listing.GetRect(entryHeight);

                if (isSelected) {
                    Widgets.DrawHighlightSelected(entryRect);
                }

                // Visual indicator for skillless work types (grayed out text)
                GUI.color = hasSkill ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                
                string label = workDef.labelShort.CapitalizeFirst();
                if (!hasSkill) label += "*"; // Asterisk indicates fallback behavior

                if (Widgets.ButtonText(entryRect, label, drawBackground: false, doMouseoverSound: true, active: true))
                {
                    if (!isSelected)
                    {
                        selectedWorkDef = workDef;
                        scrollPositionRight = Vector2.zero; // Reset scroll for new selection
                        SoundDefOf.Click?.PlayOneShotOnCamera();
                    }
                }
                
                GUI.color = Color.white;
                
                string tooltip = hasSkill 
                    ? "AWA_WorkTypeSelect_Tooltip".Translate(workDef.labelShort)
                    : "AWA_WorkTypeNoSkill_Tooltip".Translate(workDef.labelShort);
                TooltipHandler.TipRegion(entryRect, tooltip);
            }

            listing.End();
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws the rule editor interface for the selected work type on the right side.
        /// Includes header, warnings, instructional text (dynamically sized), and the dynamic list of rules.
        /// </summary>
        /// <param name="rect">The area allocated for the editor.</param>
        /// <param name="workDef">The currently selected work type.</param>
        private void DrawRuleEditor(Rect rect, WorkTypeDef workDef)
        {
            if (ruleManager == null) {
                Widgets.Label(rect.ContractedBy(10f), "Error: Rule manager reference lost.");
                return;
            }
            
            Widgets.DrawMenuSection(rect);

            // Ensure the dictionary entry exists for this work type
            if (!ruleManager.workTypeRules.ContainsKey(workDef))
            {
                ruleManager.workTypeRules[workDef] = new List<SkillPriorityRule>();
            }
            List<SkillPriorityRule> rules = ruleManager.workTypeRules[workDef];

            // --- Header Section ---
            float headerHeight = 35f;
            Rect headerRect = new Rect(rect.x + 10f, rect.y, rect.width - 20f, headerHeight);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - 220f, headerHeight), workDef.label.CapitalizeFirst());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // "Exclude Pawns" button
            Rect excludePawnsButtonRect = new Rect(headerRect.xMax - 210f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f);
            if (Widgets.ButtonText(excludePawnsButtonRect, "AWA_ExcludePawnsButton".Translate()))
            {
                var saveData = Current.Game?.GetComponent<AutomatedWork_SaveData>();
                if (saveData != null)
                {
                    Find.WindowStack.Add(new Dialog_ManageJobExclusions(saveData, workDef));
                }
            }
            TooltipHandler.TipRegion(excludePawnsButtonRect, "AWA_ExcludePawns_Tooltip".Translate());
            
            // "Add Rule" button
            Rect addButtonRect = new Rect(headerRect.xMax - 100f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f);
            if (Widgets.ButtonText(addButtonRect, "AWA_ExpertMode_AddRule".Translate()))
            {
                rules.Add(new SkillPriorityRule(0, 5, 4));
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
            TooltipHandler.TipRegion(addButtonRect, "AWA_AddRule_Tooltip".Translate());

            // --- Warning Section (for skill-less jobs) ---
            bool hasRelevantSkill = workDef.relevantSkills != null && workDef.relevantSkills.Any();
            float warningHeight = 0f;
            
            if (!hasRelevantSkill)
            {
                warningHeight = 60f;
                Rect warningRect = new Rect(headerRect.x, headerRect.yMax + 5f, headerRect.width, warningHeight);
                GUI.color = new Color(1f, 0.8f, 0f); // Yellow warning color
                Widgets.Label(warningRect, "AWA_NoSkillWarning".Translate(workDef.labelShort));
                GUI.color = Color.white;
            }
            
            // --- Info Text Section ---
            string infoText = "AWA_ExpertMode_Info".Translate();

            // Calculate exact height needed for text to avoid overlap with sliders below.
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            float infoHeight = Text.CalcHeight(infoText, headerRect.width) + 10f; // +10f buffer
            Text.Font = prevFont;

            // Enforce a sensible minimum height
            infoHeight = Mathf.Max(infoHeight, 40f);

            Rect infoRect = new Rect(headerRect.x, headerRect.yMax + warningHeight + 5f, headerRect.width, infoHeight);
            GUI.color = new Color(0.8f, 0.8f, 1f); // Light blue for info
            Widgets.Label(infoRect, infoText);
            GUI.color = Color.white;
            
            // --- Rules Scroll View ---
            // Start position is dynamically based on infoRect.yMax to ensure no overlap
            float scrollStartY = infoRect.yMax + 10f;
            Rect scrollOuterRect = new Rect(rect.x, scrollStartY, rect.width, rect.height - (scrollStartY - rect.y));
            
            float ruleRowHeight = 65f;
            float rowSpacing = 4f;
            float viewHeight = Mathf.Max(rules.Count * (ruleRowHeight + rowSpacing), scrollOuterRect.height);
            Rect viewRect = new Rect(0f, 0f, scrollOuterRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollOuterRect, ref scrollPositionRight, viewRect, true);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            SkillPriorityRule ruleToDelete = null;

            if (!rules.Any())
            {
                listing.Label("AWA_ExpertMode_NoRulesDefined".Translate());
            }
            else
            {
                int maxSkillCap = SkillPriorityRule.MaxSkillLevel;
                
                for(int i=0; i < rules.Count; i++)
                {
                    SkillPriorityRule rule = rules[i];
                    if (rule == null) continue;

                    Rect rowRect = listing.GetRect(ruleRowHeight);

                    // Layout calculations for rule row elements
                    float totalDrawableWidth = rowRect.width;
                    float deleteButtonWidth = 24f;
                    float deleteButtonPadding = 5f;
                    float priorityWidthRatio = 0.25f;
                    float availableWidthForSliders = totalDrawableWidth - deleteButtonWidth - deleteButtonPadding * 2;
                    float priorityAreaWidth = availableWidthForSliders * priorityWidthRatio;
                    float skillAreaWidth = availableWidthForSliders - priorityAreaWidth;
                    float sliderHeight = 24f;
                    float verticalSpacing = 5f;
                    float internalPadding = 5f;

                    // Define rects
                    Rect skillAreaRect = new Rect(rowRect.x + internalPadding, rowRect.y, skillAreaWidth - internalPadding*2, rowRect.height);
                    Rect priorityAreaRect = new Rect(skillAreaRect.xMax + internalPadding, rowRect.y, priorityAreaWidth - internalPadding*2, rowRect.height);
                    Rect deleteRect = new Rect(priorityAreaRect.xMax + internalPadding + deleteButtonPadding, rowRect.y + (rowRect.height - deleteButtonWidth)/2f, deleteButtonWidth, deleteButtonWidth);

                    float skillSliderYStart = skillAreaRect.y + (skillAreaRect.height - (sliderHeight * 2 + verticalSpacing)) / 2f;
                    
                    // Min Skill Slider
                    Rect minSliderRect = new Rect(skillAreaRect.x, skillSliderYStart, skillAreaRect.width, sliderHeight);
                    string minLabel = $"Min Skill: {rule.MinSkill}";
                    rule.MinSkill = (int)Widgets.HorizontalSlider(minSliderRect, rule.MinSkill, 0f, (float)maxSkillCap, true, minLabel, null, null, 1f);
                    
                    // Max Skill Slider
                    Rect maxSliderRect = new Rect(skillAreaRect.x, minSliderRect.yMax + verticalSpacing, skillAreaRect.width, sliderHeight);
                    string maxLabel = $"Max Skill: {rule.MaxSkill}";
                    rule.MaxSkill = (int)Widgets.HorizontalSlider(maxSliderRect, rule.MaxSkill, 0f, (float)maxSkillCap, true, maxLabel, null, null, 1f);
                    
                    // Input Validation: Ensure Min <= Max
                    if (rule.MinSkill > rule.MaxSkill) rule.MinSkill = rule.MaxSkill;
                    if (rule.MaxSkill < rule.MinSkill) rule.MaxSkill = rule.MinSkill;

                    // Priority Slider
                    Rect prioritySliderRect = new Rect(priorityAreaRect.x, priorityAreaRect.y + (priorityAreaRect.height - sliderHeight) / 2f, priorityAreaRect.width, sliderHeight);
                    string priorityLabel = "P:" + rule.Priority;
                    rule.Priority = (int)Widgets.HorizontalSlider(prioritySliderRect, rule.Priority, 1f, 4f, true, priorityLabel, null, null, 1f);
                    TooltipHandler.TipRegion(prioritySliderRect, "AWA_ExpertMode_Priority".Translate());

                    // Delete Button
                    if (Widgets.ButtonImage(deleteRect, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor)) {
                        ruleToDelete = rule;
                    }
                    TooltipHandler.TipRegion(deleteRect, "AWA_ExpertMode_DeleteRule".Translate());

                    listing.Gap(rowSpacing);
                }
            }
            
            if (ruleToDelete != null) {
                rules.Remove(ruleToDelete);
                // Maintain list order by Min Skill
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}