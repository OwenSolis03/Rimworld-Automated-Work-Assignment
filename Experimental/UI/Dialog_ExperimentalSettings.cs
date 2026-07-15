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
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
#if RIMWORLD_1_6
            listing.Label("Experimental Feature: Player Profiling");
#elif RIMWORLD_1_5
            Rect rect1 = listing.GetRect(Text.CalcHeight("Experimental Feature: Player Profiling", listing.ColumnWidth));
            Widgets.Label(rect1, "Experimental Feature: Player Profiling");
            listing.Gap(listing.verticalSpacing);
#endif
            Text.Font = GameFont.Small;

            listing.GapLine();

            GUI.color = new Color(1f, 0.4f, 0.4f);
#if RIMWORLD_1_6
            listing.Label("WARNING: This feature collects context data (Biome, Resources, Temperature) when you manually assign priorities to create a profile of your playstyle. It may impact game performance.");
#elif RIMWORLD_1_5
            Rect rect2 = listing.GetRect(Text.CalcHeight("WARNING: This feature collects context data (Biome, Resources, Temperature) when you manually assign priorities to create a profile of your playstyle. It may impact game performance.", listing.ColumnWidth));
            Widgets.Label(rect2, "WARNING: This feature collects context data (Biome, Resources, Temperature) when you manually assign priorities to create a profile of your playstyle. It may impact game performance.");
            listing.Gap(listing.verticalSpacing);
#endif
            GUI.color = Color.white;

            listing.Gap();

            listing.CheckboxLabeled(
                "Enable Experimental Heuristics", 
                ref saveData.enableExperimentalHeuristics,
                "Activates the profiling and heuristic assignment logic."
            );

            if (saveData.enableExperimentalHeuristics)
            {
                listing.Gap();
#if RIMWORLD_1_6
                listing.Label($"Update Frequency: Every {saveData.heuristicsUpdateFrequencyHours} hours");
#elif RIMWORLD_1_5
                string freqText = $"Update Frequency: Every {saveData.heuristicsUpdateFrequencyHours} hours";
                Rect rect3 = listing.GetRect(Text.CalcHeight(freqText, listing.ColumnWidth));
                Widgets.Label(rect3, freqText);
                listing.Gap(listing.verticalSpacing);
#endif
                
                float freq = listing.Slider((float)saveData.heuristicsUpdateFrequencyHours, 1f, 48f);
                saveData.heuristicsUpdateFrequencyHours = Mathf.RoundToInt(freq);
                
#if RIMWORLD_1_6
                listing.Label("<color=gray>Lower values mean it reacts faster to situation changes but consumes more CPU.</color>");
#elif RIMWORLD_1_5
                Rect rect4 = listing.GetRect(Text.CalcHeight("<color=gray>Lower values mean it reacts faster to situation changes but consumes more CPU.</color>", listing.ColumnWidth));
                Widgets.Label(rect4, "<color=gray>Lower values mean it reacts faster to situation changes but consumes more CPU.</color>");
                listing.Gap(listing.verticalSpacing);
#endif
            }

            listing.End();
            
            Rect closeButtonRect = new Rect(inRect.width - CloseButSize.x, inRect.height - CloseButSize.y, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(closeButtonRect, "CloseButton".Translate()))
            {
                this.Close();
            }
        }
    }
}
