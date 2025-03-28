using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    public class Dialog_ManageExclusions : Window
    {
        private readonly AutomatedWorkSettings settings;
        private Vector2 scrollPosition = Vector2.zero;
        private List<Pawn> availablePawns;

        public override Vector2 InitialSize => new Vector2(400f, 600f);


        public Dialog_ManageExclusions(AutomatedWorkSettings currentSettings)
        {
            settings = currentSettings;
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;


            this.optionalTitle = "AWA_ExclusionDialogTitle".Translate();

            RefreshPawnList();
        }

        private void RefreshPawnList()
        {
            availablePawns = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?
                .Where(p => p != null && !p.DevelopmentalStage.Baby())
                .OrderBy(p => p.LabelCap)
                .ToList() ?? new List<Pawn>();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("AWA_ExclusionDialogDesc".Translate());
            listing.GapLine();

            Rect scrollViewOutRect = listing.GetRect(inRect.height - 100f);
            float viewHeight = availablePawns.Count * 32f;
            Rect scrollViewViewRect = new Rect(0f, 0f, scrollViewOutRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollViewOutRect, ref scrollPosition, scrollViewViewRect);

            float currentY = 0f;
            foreach (Pawn pawn in availablePawns)
            {
                if (pawn == null) continue;

                string pawnId = pawn.ThingID;
                bool isExcluded = settings.excludedPawnIDs?.Contains(pawnId) ?? false;

                Rect rowRect = new Rect(0f, currentY, scrollViewViewRect.width, 30f);
                bool checkboxState = isExcluded;

                Widgets.CheckboxLabeled(rowRect, pawn.LabelCap, ref checkboxState);

                if (checkboxState != isExcluded)
                {
                    if (checkboxState)
                    {
                        if (settings.excludedPawnIDs == null) settings.excludedPawnIDs = new List<string>();
                        if (!settings.excludedPawnIDs.Contains(pawnId)) settings.excludedPawnIDs.Add(pawnId);
                    }
                    else
                    {
                        settings.excludedPawnIDs?.Remove(pawnId);
                    }
                }
                currentY += 32f;
            }

            Widgets.EndScrollView();
            listing.End();
        }
    }
}