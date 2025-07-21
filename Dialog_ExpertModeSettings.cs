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
    /// It displays a list of available work types and allows users to define skill range brackets
    /// and assign specific work priorities (1-4) to them for each selected work type.
    /// The rules configured here are managed by the <see cref="ExpertModeRuleManager"/>.
    /// </summary>
    public class Dialog_ExpertModeSettings : Window
    {
        /// <summary>
        /// Reference to the rule manager component attached to the current game instance.
        /// Used to access and modify the expert mode rules.
        /// </summary>
        private ExpertModeRuleManager ruleManager;

        /// <summary>
        /// Stores the current vertical scroll position for the left panel, which lists the work types.
        /// </summary>
        private Vector2 scrollPositionLeft = Vector2.zero;
        
        /// <summary>
        /// Stores the current vertical scroll position for the right panel, which displays the rule editor.
        /// </summary>
        private Vector2 scrollPositionRight = Vector2.zero;
        
        /// <summary>
        /// Holds the currently selected work type definition for which rules are being displayed or edited.
        /// </summary>
        private WorkTypeDef selectedWorkDef = null;

        /// <summary>
        /// A cached list of all relevant <see cref="WorkTypeDef"/>s available in the current game state.
        /// </summary>
        private List<WorkTypeDef> relevantWorkTypesCache = new List<WorkTypeDef>();

        /// <summary>
        /// Gets the initial dimensions of the dialog window when it first opens.
        /// </summary>
        public override Vector2 InitialSize => new Vector2(800f, 600f);

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialog_ExpertModeSettings"/> class.
        /// Sets up standard window properties, retrieves the active rule manager,
        /// populates the cache of relevant work types, and sets the window title.
        /// </summary>
        public Dialog_ExpertModeSettings()
        {
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;

            ruleManager = Current.Game?.GetComponent<ExpertModeRuleManager>();

            relevantWorkTypesCache = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                .OrderBy(wtd => wtd.labelShort)
                .ToList();

            this.optionalTitle = "AWA_ExpertMode_RuleWindowTitle".Translate();
        }

        /// <summary>
        /// Called by the game before the window is displayed. Ensures a work type is selected by default.
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen();
            if (selectedWorkDef == null && relevantWorkTypesCache.Any())
            {
                selectedWorkDef = relevantWorkTypesCache.First();
            }
            else if (selectedWorkDef != null && !relevantWorkTypesCache.Contains(selectedWorkDef))
            {
                selectedWorkDef = relevantWorkTypesCache.FirstOrDefault();
            }
        }
        
        /// <summary>
        /// Draws the main interactive content of the dialog window.
        /// This method orchestrates the drawing of the left (list) and right (editor) panels.
        /// </summary>
        /// <param name="inRect">The rectangle area available for drawing the window content.</param>
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

            Rect closeButtonRect = new Rect(inRect.width - CloseButSize.x - 10f , inRect.height - CloseButSize.y - 5f , CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
            {
                this.Close();
            }
        }

        /// <summary>
        /// Draws the scrollable list of selectable work types in the left panel.
        /// </summary>
        /// <param name="rect">The rectangle area for the left panel.</param>
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
                Rect entryRect = listing.GetRect(entryHeight);

                if (isSelected) {
                    Widgets.DrawHighlightSelected(entryRect);
                }

                if (Widgets.ButtonText(entryRect, workDef.labelShort.CapitalizeFirst(), drawBackground: false, doMouseoverSound: true, active: true))
                {
                    if (!isSelected)
                    {
                        selectedWorkDef = workDef;
                        scrollPositionRight = Vector2.zero;
                        SoundDefOf.Click?.PlayOneShotOnCamera();
                    }
                }
                
                TooltipHandler.TipRegion(entryRect, $"Select {workDef.labelShort} to edit rules.");
            }

            listing.End();
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws the rule editor interface in the right panel for the currently selected work type.
        /// </summary>
        /// <param name="rect">The rectangle area for the right panel.</param>
        /// <param name="workDef">The currently selected work type.</param>
        private void DrawRuleEditor(Rect rect, WorkTypeDef workDef)
        {
            if (ruleManager == null) {
                Widgets.Label(rect.ContractedBy(10f), "Error: Rule manager reference lost.");
                return;
            }
            
            Widgets.DrawMenuSection(rect);

            if (!ruleManager.workTypeRules.ContainsKey(workDef))
            {
                ruleManager.workTypeRules[workDef] = new List<SkillPriorityRule>();
            }
            List<SkillPriorityRule> rules = ruleManager.workTypeRules[workDef];

            float headerHeight = 35f;
            Rect headerRect = new Rect(rect.x + 10f, rect.y, rect.width - 20f, headerHeight);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - 110f, headerHeight), workDef.label.CapitalizeFirst());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            Rect addButtonRect = new Rect(headerRect.xMax - 100f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f);
            if (Widgets.ButtonText(addButtonRect, "AWA_ExpertMode_AddRule".Translate()))
            {
                rules.Add(new SkillPriorityRule(0, 5, 4));
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
            // Resolviendo el 'TODO' que tenías:
            TooltipHandler.TipRegion(addButtonRect, "Adds a new skill-based priority rule for this work type.");

            Rect scrollOuterRect = new Rect(rect.x, rect.y + headerHeight, rect.width, rect.height - headerHeight);
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
                for(int i=0; i < rules.Count; i++)
                {
                    SkillPriorityRule rule = rules[i];
                    if (rule == null) continue;

                    Rect rowRect = listing.GetRect(ruleRowHeight);

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

                    Rect skillAreaRect = new Rect(rowRect.x + internalPadding, rowRect.y, skillAreaWidth - internalPadding*2, rowRect.height);
                    Rect priorityAreaRect = new Rect(skillAreaRect.xMax + internalPadding, rowRect.y, priorityAreaWidth - internalPadding*2, rowRect.height);
                    Rect deleteRect = new Rect(priorityAreaRect.xMax + internalPadding + deleteButtonPadding, rowRect.y + (rowRect.height - deleteButtonWidth)/2f, deleteButtonWidth, deleteButtonWidth);

                    float skillSliderYStart = skillAreaRect.y + (skillAreaRect.height - (sliderHeight * 2 + verticalSpacing)) / 2f;
                    
                    Rect minSliderRect = new Rect(skillAreaRect.x, skillSliderYStart, skillAreaRect.width, sliderHeight);
                    string minLabel = $"Min Skill: {rule.MinSkill}";
                    rule.MinSkill = (int)Widgets.HorizontalSlider(minSliderRect, rule.MinSkill, 0f, 20f, true, minLabel, null, null, 1f);
                    
                    Rect maxSliderRect = new Rect(skillAreaRect.x, minSliderRect.yMax + verticalSpacing, skillAreaRect.width, sliderHeight);
                    string maxLabel = $"Max Skill: {rule.MaxSkill}";
                    rule.MaxSkill = (int)Widgets.HorizontalSlider(maxSliderRect, rule.MaxSkill, 0f, 20f, true, maxLabel, null, null, 1f);
                    
                    if (rule.MinSkill > rule.MaxSkill) rule.MinSkill = rule.MaxSkill;
                    if (rule.MaxSkill < rule.MinSkill) rule.MaxSkill = rule.MinSkill;

                    Rect prioritySliderRect = new Rect(priorityAreaRect.x, priorityAreaRect.y + (priorityAreaRect.height - sliderHeight) / 2f, priorityAreaRect.width, sliderHeight);
                    string priorityLabel = "P:" + rule.Priority;
                    rule.Priority = (int)Widgets.HorizontalSlider(prioritySliderRect, rule.Priority, 1f, 4f, true, priorityLabel, null, null, 1f);
                    TooltipHandler.TipRegion(prioritySliderRect, "AWA_ExpertMode_Priority".Translate());

                    if (Widgets.ButtonImage(deleteRect, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor)) {
                        ruleToDelete = rule;
                    }
                    TooltipHandler.TipRegion(deleteRect, "AWA_ExpertMode_DeleteRule".Translate());

                    listing.Gap(rowSpacing);
                }
            }
            
            if (ruleToDelete != null) {
                rules.Remove(ruleToDelete);
            }

            listing.End();
            Widgets.EndScrollView();
            
            if (ruleToDelete != null) {
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
        }
    }
}