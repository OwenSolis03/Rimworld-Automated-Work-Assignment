using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace Automated_Work_Assignment.Experimental.UI
{
    public class Dialog_ExperimentalSettings : Window
    {
        private AutomatedWork_SaveData saveData;

        // --- Position/Size Persistence ---
        private static Rect? savedWindowRect = null;

        public override Vector2 InitialSize => new Vector2(500f, 350f);

        public Dialog_ExperimentalSettings(AutomatedWork_SaveData saveData)
        {
            this.saveData = saveData;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;
            optionalTitle = "Experimental Heuristics Module";
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

        public override void DoWindowContents(Rect inRect)
        {
            // Keep content clear of the bottom close button so they never overlap.
            float footerHeight = CloseButSize.y + 10f;
            Rect contentRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - footerHeight);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(contentRect);

            Text.Font = GameFont.Medium;
#if RIMWORLD_1_6
            listing.Label("AWA_Experimental_Title".Translate());
#elif RIMWORLD_1_5
            Rect rect1 = listing.GetRect(Text.CalcHeight("AWA_Experimental_Title".Translate(), listing.ColumnWidth));
            Widgets.Label(rect1, "AWA_Experimental_Title".Translate());
            listing.Gap(listing.verticalSpacing);
#endif
            Text.Font = GameFont.Small;

            listing.GapLine();

            GUI.color = new Color(1f, 0.4f, 0.4f);
#if RIMWORLD_1_6
            listing.Label("AWA_Experimental_Warning".Translate());
#elif RIMWORLD_1_5
            Rect rect2 = listing.GetRect(Text.CalcHeight("AWA_Experimental_Warning".Translate(), listing.ColumnWidth));
            Widgets.Label(rect2, "AWA_Experimental_Warning".Translate());
            listing.Gap(listing.verticalSpacing);
#endif
            GUI.color = Color.white;

            listing.Gap();

            listing.CheckboxLabeled(
                "AWA_EnableExperimentalHeuristics".Translate(), 
                ref saveData.enableExperimentalHeuristics,
                "AWA_EnableExperimentalHeuristics_Tooltip".Translate()
            );

            if (saveData.enableExperimentalHeuristics)
            {
                listing.Gap();
#if RIMWORLD_1_6
                listing.Label("AWA_Experimental_UpdateFreq".Translate(saveData.heuristicsUpdateFrequencyHours));
#elif RIMWORLD_1_5
                string freqText = "AWA_Experimental_UpdateFreq".Translate(saveData.heuristicsUpdateFrequencyHours);
                Rect rect3 = listing.GetRect(Text.CalcHeight(freqText, listing.ColumnWidth));
                Widgets.Label(rect3, freqText);
                listing.Gap(listing.verticalSpacing);
#endif
                
                float freq = listing.Slider((float)saveData.heuristicsUpdateFrequencyHours, 1f, 48f);
                saveData.heuristicsUpdateFrequencyHours = Mathf.RoundToInt(freq);

                GUI.color = Color.gray;
#if RIMWORLD_1_6
                listing.Label("AWA_Experimental_CPUWarning".Translate());
#elif RIMWORLD_1_5
                Rect rect4 = listing.GetRect(Text.CalcHeight("AWA_Experimental_CPUWarning".Translate(), listing.ColumnWidth));
                Widgets.Label(rect4, "AWA_Experimental_CPUWarning".Translate());
                listing.Gap(listing.verticalSpacing);
#endif
                GUI.color = Color.white;
            }

            listing.End();
            
            Rect closeButtonRect = new Rect(inRect.width - CloseButSize.x - 5f, inRect.height - CloseButSize.y - 5f, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
            {
                this.Close();
            }
        }
    }
}
