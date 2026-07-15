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
            listing.Label("Experimental Feature: Player Profiling");
            Text.Font = GameFont.Small;

            listing.GapLine();

            GUI.color = new Color(1f, 0.4f, 0.4f);
            listing.Label("WARNING: This feature collects context data (Biome, Resources, Temperature) when you manually assign priorities to create a profile of your playstyle. It may impact game performance.");
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
                listing.Label($"Update Frequency: Every {saveData.heuristicsUpdateFrequencyHours} hours");
                
                float freq = listing.Slider((float)saveData.heuristicsUpdateFrequencyHours, 1f, 48f);
                saveData.heuristicsUpdateFrequencyHours = Mathf.RoundToInt(freq);
                
                listing.Label("<color=gray>Lower values mean it reacts faster to situation changes but consumes more CPU.</color>");
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
