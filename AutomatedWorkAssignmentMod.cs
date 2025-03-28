using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    public class AutomatedWorkAssignmentMod : Mod
    {

        public static AutomatedWorkSettings Settings;
        private Dictionary<string, string> countBuffers = new Dictionary<string, string>();
        private Dictionary<string, string> priorityBuffers = new Dictionary<string, string>();
        private Vector2 scrollPosition = Vector2.zero;

        public AutomatedWorkAssignmentMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AutomatedWorkSettings>();
        }

        public override string SettingsCategory() => "AWA_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);


            listingStandard.CheckboxLabeled("AWA_EnableModLabel".Translate(), ref Settings.modEnabled, "AWA_EnableModTooltip".Translate());


            listingStandard.CheckboxLabeled("Enable Automatic Daily Refresh", ref Settings.enableDailyRefresh, "If enabled, assignments will be refreshed automatically near the start of each day.");


            listingStandard.GapLine(12f);

            if (listingStandard.ButtonText("AWA_ManageExclusionsButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ManageExclusions(Settings));
            }

            listingStandard.GapLine(12f);

            listingStandard.Label("AWA_DesiredPawnsLabel".Translate());

            // --- ScrollView (Sin cambios en su lógica interna) ---
            float currentYPos = listingStandard.CurHeight;
            float remainingHeight = inRect.height - currentYPos - 10f;
            if (remainingHeight < 100f) remainingHeight = 100f;
            Rect outRect = new Rect(inRect.x, currentYPos, inRect.width, remainingHeight);

            List<WorkTypeDef> relevantWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd.workTags != WorkTags.None)
                .OrderBy(wtd => wtd.labelShort)
                .ToList();

            float rowHeight = 50f;
            float totalContentHeight = relevantWorkTypes.Count * rowHeight;
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, totalContentHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            Listing_Standard scrollListing = new Listing_Standard(GameFont.Small);
            scrollListing.Begin(viewRect);

            foreach (WorkTypeDef workDef in relevantWorkTypes)
            {
                string defName = workDef.defName;
                WorkSettingValues currentSetting = Settings.GetWorkSetting(defName);

                Rect rowRect = scrollListing.GetRect(rowHeight - scrollListing.verticalSpacing);
                Rect labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width * 0.4f, 30f);
                Widgets.Label(labelRect, workDef.labelShort.CapitalizeFirst());

                float controlWidth = rowRect.width * 0.25f;
                float spacing = 10f;

                Rect countLabelRect = new Rect(labelRect.xMax + spacing, rowRect.y, 50f, 30f);
                Widgets.Label(countLabelRect, "AWA_CountLabel".Translate());
                Rect countFieldRect = new Rect(countLabelRect.xMax, rowRect.y, controlWidth - 50f, 30f);

                if (!countBuffers.ContainsKey(defName)) { countBuffers[defName] = currentSetting.count.ToString(); }
                string countBuffer = countBuffers[defName];
                int countBefore = currentSetting.count;
                Widgets.TextFieldNumeric<int>(countFieldRect, ref currentSetting.count, ref countBuffer, 0, 999);
                if (currentSetting.count != countBefore) { countBuffers[defName] = currentSetting.count.ToString(); }
                else { if (countBuffer != currentSetting.count.ToString()) { countBuffers[defName] = currentSetting.count.ToString(); } }
                if (currentSetting.count < 0) currentSetting.count = 0;

                Rect priorityLabelRect = new Rect(countFieldRect.xMax + spacing, rowRect.y, 60f, 30f);
                Widgets.Label(priorityLabelRect, "AWA_PriorityFieldLabel".Translate());
                Rect priorityFieldRect = new Rect(priorityLabelRect.xMax, rowRect.y, controlWidth - 60f, 30f);

                if (!priorityBuffers.ContainsKey(defName)) { priorityBuffers[defName] = currentSetting.priority.ToString(); }
                string priorityBuffer = priorityBuffers[defName];
                int priorityBefore = currentSetting.priority;
                Widgets.TextFieldNumeric<int>(priorityFieldRect, ref currentSetting.priority, ref priorityBuffer, 1, 4);
                if (currentSetting.priority != priorityBefore) { priorityBuffers[defName] = currentSetting.priority.ToString(); }
                else { if (priorityBuffer != currentSetting.priority.ToString()) { priorityBuffers[defName] = currentSetting.priority.ToString(); } }
                if (currentSetting.priority < 1) currentSetting.priority = 1;
                if (currentSetting.priority > 4) currentSetting.priority = 4;
            }

            scrollListing.End();
            Widgets.EndScrollView();
            listingStandard.End();
        }

        public override void WriteSettings() => base.WriteSettings();
    }
}