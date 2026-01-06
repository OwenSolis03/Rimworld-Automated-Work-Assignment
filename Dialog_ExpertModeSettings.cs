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
    /// SIMPLIFIED: No longer includes count/percentage sliders - uses Simple Mode settings.
    /// </summary>
    public class Dialog_ExpertModeSettings : Window
    {
        private ExpertModeRuleManager ruleManager;
        private Vector2 scrollPositionLeft = Vector2.zero;
        private Vector2 scrollPositionRight = Vector2.zero;
        private WorkTypeDef selectedWorkDef = null;
        private List<WorkTypeDef> relevantWorkTypesCache = new List<WorkTypeDef>();

        public override Vector2 InitialSize => new Vector2(800f, 600f);

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

            // Dynamic title showing skill cap if extended
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

        public override void PreOpen()
        {
            base.PreOpen();
            if (selectedWorkDef == null && relevantWorkTypesCache.Any())
            {
                // Pre-select first work type with associated skill (skip skillless jobs)
                selectedWorkDef = relevantWorkTypesCache
                    .FirstOrDefault(w => w.relevantSkills != null && w.relevantSkills.Any())
                    ?? relevantWorkTypesCache.First();
            }
            else if (selectedWorkDef != null && !relevantWorkTypesCache.Contains(selectedWorkDef))
            {
                selectedWorkDef = relevantWorkTypesCache.FirstOrDefault();
            }
        }
        
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

            Rect closeButtonRect = new Rect(inRect.width - CloseButSize.x - 10f, inRect.height - CloseButSize.y - 5f, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
            {
                this.Close();
            }
        }

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

                // Visual indicator for skillless work types
                GUI.color = hasSkill ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                
                string label = workDef.labelShort.CapitalizeFirst();
                if (!hasSkill) label += "*"; // FIX: Usar asterisco en lugar de paréntesis

                if (Widgets.ButtonText(entryRect, label, drawBackground: false, doMouseoverSound: true, active: true))
                {
                    if (!isSelected)
                    {
                        selectedWorkDef = workDef;
                        scrollPositionRight = Vector2.zero;
                        SoundDefOf.Click?.PlayOneShotOnCamera();
                    }
                }
                
                GUI.color = Color.white;
                
                string tooltip = hasSkill 
                    ? $"Select {workDef.labelShort} to edit rules." 
                    : $"{workDef.labelShort} has no associated skill. Expert rules will use Social skill as fallback.";
                TooltipHandler.TipRegion(entryRect, tooltip);
            }

            listing.End();
            Widgets.EndScrollView();
        }

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
            Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - 220f, headerHeight), workDef.label.CapitalizeFirst());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Button to exclude specific pawns from this job
            Rect excludePawnsButtonRect = new Rect(headerRect.xMax - 210f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f);
            if (Widgets.ButtonText(excludePawnsButtonRect, "Exclude Pawns"))
            {
                var saveData = Current.Game?.GetComponent<AutomatedWork_SaveData>();
                if (saveData != null)
                {
                    Find.WindowStack.Add(new Dialog_ManageJobExclusions(saveData, workDef));
                }
            }
            TooltipHandler.TipRegion(excludePawnsButtonRect, "Select specific pawns to exclude from this job type.");
            
            Rect addButtonRect = new Rect(headerRect.xMax - 100f, headerRect.y + (headerHeight - 30f)/2f, 100f, 30f);
            if (Widgets.ButtonText(addButtonRect, "AWA_ExpertMode_AddRule".Translate()))
            {
                rules.Add(new SkillPriorityRule(0, 5, 4));
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }
            TooltipHandler.TipRegion(addButtonRect, "Adds a new skill-based priority rule for this work type.");

            // Warning for skillless work types
            bool hasRelevantSkill = workDef.relevantSkills != null && workDef.relevantSkills.Any();
            float warningHeight = 0f;
            
            if (!hasRelevantSkill)
            {
                warningHeight = 60f;
                Rect warningRect = new Rect(headerRect.x, headerRect.yMax + 5f, headerRect.width, warningHeight);
                GUI.color = new Color(1f, 0.8f, 0f);
                Widgets.Label(warningRect, 
                    $"⚠ WARNING: {workDef.labelShort} has no skill associated.\n" +
                    "Expert Mode rules will use Social skill as fallback.\n" +
                    "Consider using Simple Mode for this work type instead.");
                GUI.color = Color.white;
            }
            
            // Info message about count/percentage
            float infoHeight = 50f;
            Rect infoRect = new Rect(headerRect.x, headerRect.yMax + warningHeight + 5f, headerRect.width, infoHeight);
            GUI.color = new Color(0.8f, 0.8f, 1f);
            Widgets.Label(infoRect, 
                "Expert Mode uses the Count/Percentage settings from the main Simple Mode sliders.\n" +
                "Rules below define which priorities (1-4) to assign based on skill levels.");
            GUI.color = Color.white;
            
            float scrollStartY = infoRect.yMax + 5f;
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
                    rule.MinSkill = (int)Widgets.HorizontalSlider(minSliderRect, rule.MinSkill, 0f, (float)maxSkillCap, true, minLabel, null, null, 1f);
                    
                    Rect maxSliderRect = new Rect(skillAreaRect.x, minSliderRect.yMax + verticalSpacing, skillAreaRect.width, sliderHeight);
                    string maxLabel = $"Max Skill: {rule.MaxSkill}";
                    rule.MaxSkill = (int)Widgets.HorizontalSlider(maxSliderRect, rule.MaxSkill, 0f, (float)maxSkillCap, true, maxLabel, null, null, 1f);
                    
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
                rules.Sort((a, b) => a.MinSkill.CompareTo(b.MinSkill));
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}