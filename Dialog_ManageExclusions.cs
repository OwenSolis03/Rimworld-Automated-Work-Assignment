using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Represents the pop-up window dialog used for managing which pawns are excluded.
    /// Now references the per-save data component (AutomatedWork_SaveData).
    /// </summary>
    public class Dialog_ManageExclusions : Window
    {
        /// <summary>
        /// A reference to the mod's per-save data component, used to read/modify the excluded pawns list.
        /// </summary>
        private readonly AutomatedWork_SaveData saveData;

        /// <summary> Stores the current vertical scroll position of the pawn list. </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary> Cached list of pawns eligible for exclusion/inclusion in this dialog. </summary>
        private List<Pawn> availablePawns;

        /// <summary> Defines the initial size of the dialog window. </summary>
        public override Vector2 InitialSize => new Vector2(400f, 600f);

        /// <summary>
        /// Constructor for the exclusion management dialog.
        /// Now accepts AutomatedWork_SaveData.
        /// </summary>
        /// <param name="currentSaveData">A reference to the current save game's data component.</param>
        public Dialog_ManageExclusions(AutomatedWork_SaveData currentSaveData)
        {
            saveData = currentSaveData ?? throw new ArgumentNullException(nameof(currentSaveData));

            // Standard window properties
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;

            this.optionalTitle = "AWA_ExclusionDialogTitle".Translate();
            RefreshPawnList();
        }

        /// <summary>
        /// Refreshes the list of pawns available for exclusion.
        /// </summary>
        private void RefreshPawnList()
        {
            try
            {
                availablePawns = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?
                                     .Where(p => p != null && !p.DevelopmentalStage.Baby())
                                     .OrderBy(p => p.LabelCap)
                                     .ToList()
                                 ?? new List<Pawn>();
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception refreshing pawn list for exclusion dialog: {ex}");
                availablePawns = new List<Pawn>();
            }
        }

        /// <summary>
        /// Draws the actual content of the dialog window.
        /// Now uses the 'saveData' field to access the excluded pawns list.
        /// </summary>
        /// <param name="inRect">The rectangle area available for drawing content.</param>
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            try {
                listing.Label("AWA_ExclusionDialogDesc".Translate());
                listing.GapLine();
            } catch (Exception ex) { Log.Error($"[AutoWork] Exception drawing top elements in exclusion dialog: {ex}"); }

            if (availablePawns == null) {
                listing.Label("Error: Could not load pawn list.");
                listing.End(); return;
            }

            // --- ScrollView Setup ---
            Rect scrollViewOutRect = default;
            Rect scrollViewViewRect = default;
            const float rowHeight = 32f;

            try {
                float currentYPos = listing.CurHeight;
                float availableHeight = inRect.height - currentYPos - 50f;
                float scrollViewHeight = Mathf.Max(100f, availableHeight);
                scrollViewOutRect = new Rect(inRect.x, currentYPos, inRect.width, scrollViewHeight);

                float viewHeight = availablePawns.Count * rowHeight;
                float viewWidth = Mathf.Max(0f, scrollViewOutRect.width - 16f);
                scrollViewViewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            } catch (Exception ex) {
                Log.Error($"[AutoWork] Exception setting up ScrollView in exclusion dialog: {ex}");
                listing.Label("Error setting up scroll view.");
                listing.End(); return;
            }

            // --- Draw ScrollView ---
            Widgets.BeginScrollView(scrollViewOutRect, ref scrollPosition, scrollViewViewRect);
            float currentY = 0f;

            foreach (Pawn pawn in availablePawns)
            {
                // --- Exception Handling for each pawn row ---
                try
                {
                    if (pawn == null) continue;
                    string pawnId = pawn.ThingID;

                    bool isExcluded = saveData.excludedPawnIDs?.Contains(pawnId) ?? false;

                    Rect rowRect = new Rect(0f, currentY, scrollViewViewRect.width, 30f);
                    bool checkboxState = isExcluded;
                    Widgets.CheckboxLabeled(rowRect, pawn.LabelCap, ref checkboxState);

                    if (checkboxState != isExcluded)
                    {
                        if (checkboxState) {
                            if (saveData.excludedPawnIDs == null) saveData.excludedPawnIDs = new List<string>();
                            if (!saveData.excludedPawnIDs.Contains(pawnId)) saveData.excludedPawnIDs.Add(pawnId);
                        } else {
                            saveData.excludedPawnIDs?.Remove(pawnId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing exclusion row for pawn '{pawn?.ThingID ?? "NULL"}': {ex}");
                }
                // --- End Exception Handling ---
                finally
                {
                    currentY += rowHeight;
                }
            }
            Widgets.EndScrollView();
            listing.End();
        }
    }
}