using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Defines the modal dialog window that allows users to select which pawns
    /// should be excluded from the automatic work assignment system.
    /// This window directly interacts with the per-save game data component (`AutomatedWork_SaveData`)
    /// to read and update the list of excluded pawns.
    /// </summary>
    public class Dialog_ManageExclusions : Window
    {
        /// <summary>
        /// A direct reference to the mod's data storage for the current save game.
        /// Used to access and modify the list of pawn IDs marked as excluded.
        /// </summary>
        private readonly AutomatedWork_SaveData saveData;

        /// <summary> Maintains the vertical scroll position within the pawn list view. </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary> A cached list of colonists currently eligible for exclusion management.
        /// This list includes all free, non-baby colonists on the current map.
        /// Refreshed when the dialog opens.
        /// </summary>
        private List<Pawn> availablePawns;

        /// <summary> Specifies the default dimensions of the dialog window when opened. </summary>
        public override Vector2 InitialSize => new Vector2(400f, 600f);

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialog_ManageExclusions"/> window.
        /// Requires the save data component for the current game session.
        /// </summary>
        /// <param name="currentSaveData">The active <see cref="AutomatedWork_SaveData"/> instance containing the exclusion list for the current save.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="currentSaveData"/> is null.</exception>
        public Dialog_ManageExclusions(AutomatedWork_SaveData currentSaveData)
        {
            saveData = currentSaveData ?? throw new ArgumentNullException(nameof(currentSaveData));

            // Standard RimWorld window configuration settings
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;

            this.optionalTitle = "AWA_ExclusionDialogTitle".Translate();
            RefreshPawnList();
        }

        /// <summary>
        /// Updates the internal list of `availablePawns` by querying the current map's state.
        /// Filters for free colonists who are not babies and sorts them alphabetically by name.
        /// Handles potential exceptions during pawn retrieval.
        /// </summary>
        private void RefreshPawnList()
        {
            try
            {
                // Fetch all free colonists currently spawned on the map,
                // exclude babies, order by name, and store them.
                availablePawns = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?
                                     .Where(p => p != null && !p.DevelopmentalStage.Baby())
                                     .OrderBy(p => p.LabelCap)
                                     .ToList()
                                 ?? new List<Pawn>(); // Fallback to an empty list if map/pawns are inaccessible.
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception refreshing pawn list for exclusion dialog: {ex}");
                availablePawns = new List<Pawn>(); // Ensure list is empty on error.
            }
        }

        /// <summary>
        /// Renders the content of the dialog window, including the description, pawn list, and checkboxes.
        /// This method is called repeatedly by the UI system while the window is open.
        /// It uses the `saveData` field to determine the current exclusion status for each pawn
        /// and updates `saveData` when checkboxes are toggled.
        /// </summary>
        /// <param name="inRect">The rectangular area within the window where content should be drawn.</param>
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            try {
                // Display introductory text and a separator line.
#if RIMWORLD_1_6
                listing.Label("AWA_ExclusionDialogDesc".Translate());
#elif RIMWORLD_1_5
                string t1 = "AWA_ExclusionDialogDesc".Translate();
                Widgets.Label(listing.GetRect(Text.CalcHeight(t1, listing.ColumnWidth)), t1);
                listing.Gap(listing.verticalSpacing);
#endif
                listing.GapLine();
            } catch (Exception ex) { Log.Error($"[AutoWork] Exception drawing top elements in exclusion dialog: {ex}"); }

            // Error handling if pawn list failed to load
            if (availablePawns == null) {
#if RIMWORLD_1_6
                listing.Label("AWA_Error_NoPawnList".Translate());
#elif RIMWORLD_1_5
                string t2 = "AWA_Error_NoPawnList".Translate();
                Widgets.Label(listing.GetRect(Text.CalcHeight(t2, listing.ColumnWidth)), t2);
                listing.Gap(listing.verticalSpacing);
#endif
                listing.End();
                return;
            }

            // --- ScrollView Setup ---
            Rect scrollViewOutRect = default;
            Rect scrollViewViewRect = default;
            const float rowHeight = 32f;

            try {
                // Calculate remaining vertical space for the scroll view
                float currentYPos = listing.CurHeight;
                float availableHeight = inRect.height - currentYPos - 50f;
                float scrollViewHeight = Mathf.Max(100f, availableHeight);
                scrollViewOutRect = new Rect(inRect.x, currentYPos, inRect.width, scrollViewHeight);

                // Calculate the total height required by all pawn rows
                float viewHeight = availablePawns.Count * rowHeight;
                // Calculate the width, accounting for the scrollbar width (16f)
                float viewWidth = Mathf.Max(0f, scrollViewOutRect.width - 16f);
                scrollViewViewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            } catch (Exception ex) {
                Log.Error($"[AutoWork] Exception setting up ScrollView in exclusion dialog: {ex}");
#if RIMWORLD_1_6
                listing.Label("AWA_Error_ScrollView".Translate());
#elif RIMWORLD_1_5
                string t3 = "AWA_Error_ScrollView".Translate();
                Widgets.Label(listing.GetRect(Text.CalcHeight(t3, listing.ColumnWidth)), t3);
                listing.Gap(listing.verticalSpacing);
#endif
                listing.End();
                return;
            }

            // --- Draw ScrollView ---
            Widgets.BeginScrollView(scrollViewOutRect, ref scrollPosition, scrollViewViewRect);
            float currentY = 0f;

            foreach (Pawn pawn in availablePawns)
            {
                try
                {
                    if (pawn == null) continue;
                    string pawnId = pawn.ThingID;

                    // Check if this pawn's ID is present in the exclusion list from save data.
                    bool isExcluded = saveData.excludedPawnIDs?.Contains(pawnId) ?? false;

                    // Define the rectangle for the current pawn's row
                    Rect rowRect = new Rect(0f, currentY, scrollViewViewRect.width, 30f);
                    bool checkboxState = isExcluded;

                    // Draw the checkbox with the pawn's label.
                    Widgets.CheckboxLabeled(rowRect, pawn.LabelCap, ref checkboxState);

                    // Detect if the checkbox state changed
                    if (checkboxState != isExcluded)
                    {
                        if (checkboxState)
                        {
                            if (saveData.excludedPawnIDs == null) saveData.excludedPawnIDs = new List<string>();
                            if (!saveData.excludedPawnIDs.Contains(pawnId)) saveData.excludedPawnIDs.Add(pawnId);
                        } else
                        {
                            saveData.excludedPawnIDs?.Remove(pawnId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing exclusion row for pawn '{pawn?.ThingID ?? "NULL"}': {ex}");
                }
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