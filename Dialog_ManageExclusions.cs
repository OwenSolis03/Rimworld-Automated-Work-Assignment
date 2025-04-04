using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld; // Needed for Window specifics

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Represents the pop-up window dialog used for managing which pawns are excluded
    /// from the automatic work assignment logic.
    /// </summary>
    public class Dialog_ManageExclusions : Window
    {
        /// <summary>
        /// A reference to the mod's settings, used to read and modify the excluded pawns list.
        /// Marked readonly as it's set in the constructor and shouldn't change afterwards.
        /// </summary>
        private readonly AutomatedWorkSettings settings;

        /// <summary>
        /// Stores the current vertical scroll position of the pawn list.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;

        /// <summary>
        /// Cached list of pawns eligible for exclusion/inclusion in this dialog.
        /// Populated by RefreshPawnList().
        /// </summary>
        private List<Pawn> availablePawns;

        /// <summary>
        /// Defines the initial size of the dialog window.
        /// </summary>
        public override Vector2 InitialSize => new Vector2(400f, 600f);

        /// <summary>
        /// Constructor for the exclusion management dialog.
        /// </summary>
        /// <param name="currentSettings">A reference to the current mod settings instance.</param>
        public Dialog_ManageExclusions(AutomatedWorkSettings currentSettings)
        {
            settings = currentSettings ?? throw new ArgumentNullException(nameof(currentSettings)); // Ensure settings are provided

            // Standard window properties
            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;

            // Set the window title using a translation key
            this.optionalTitle = "AWA_ExclusionDialogTitle".Translate();

            // Populate the list of pawns when the dialog is created
            RefreshPawnList();
        }

        /// <summary>
        /// Refreshes the list of pawns available for exclusion.
        /// Filters for free colonists on the current map, excluding babies.
        /// Orders the list alphabetically by pawn name.
        /// Handles cases where the map or pawn list might be null.
        /// </summary>
        private void RefreshPawnList()
        {
            try
            {
                // Safely access map pawns and filter
                availablePawns = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?
                                     .Where(p => p != null && !p.DevelopmentalStage.Baby())
                                     // --- CORRECCIÓN: Revertir a OrderBy(p => p.LabelCap) ---
                                     .OrderBy(p => p.LabelCap) // Order by name using TaggedString directly
                                     // --- FIN CORRECCIÓN ---
                                     .ToList()
                                 ?? new List<Pawn>(); // If any part of the chain is null, default to an empty list
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception refreshing pawn list for exclusion dialog: {ex}");
                availablePawns = new List<Pawn>(); // Ensure list is initialized even on error
            }
        }

        /// <summary>
        /// Draws the actual content of the dialog window.
        /// Overrides the base Window method.
        /// </summary>
        /// <param name="inRect">The rectangle area available for drawing content.</param>
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            try // General catch for top-level UI elements
            {
                listing.Label("AWA_ExclusionDialogDesc".Translate()); // Display description text
                listing.GapLine(); // Draw a separator line
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception drawing top elements in exclusion dialog: {ex}");
            }

            // Check if the pawn list is valid before proceeding
            if (availablePawns == null)
            {
                listing.Label("Error: Could not load pawn list.");
                Log.ErrorOnce("[AutoWork] availablePawns list is null in DoWindowContents.", 1984775);
                listing.End();
                return;
            }

            // --- ScrollView Setup ---
            Rect scrollViewOutRect = default;
            Rect scrollViewViewRect = default;
            const float rowHeight = 32f; // Height per pawn row + spacing (use const)

            try // Exception handling for ScrollView setup
            {
                 // Define the outer rectangle for the scroll view area
                float currentYPos = listing.CurHeight;
                float availableHeight = inRect.height - currentYPos - 50f; // Reserve space at bottom
                float scrollViewHeight = Mathf.Max(100f, availableHeight); // Min height 100
                scrollViewOutRect = new Rect(inRect.x, currentYPos, inRect.width, scrollViewHeight);

                // Calculate the total height needed for all pawn rows
                float viewHeight = availablePawns.Count * rowHeight;
                // Define the inner rectangle representing the total scrollable content size
                float viewWidth = Mathf.Max(0f, scrollViewOutRect.width - 16f); // Subtract scrollbar width safely
                scrollViewViewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            }
             catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception setting up ScrollView in exclusion dialog: {ex}");
                listing.Label("Error setting up scroll view.");
                listing.End();
                return;
            }


            // --- Draw ScrollView ---
            Widgets.BeginScrollView(scrollViewOutRect, ref scrollPosition, scrollViewViewRect);

            float currentY = 0f; // Manual tracking of vertical position within the scroll view

            // Iterate through each available pawn
            foreach (Pawn pawn in availablePawns)
            {
                // --- Exception Handling for each pawn row ---
                try
                {
                    if (pawn == null) continue;

                    string pawnId = pawn.ThingID;
                    bool isExcluded = settings.excludedPawnIDs?.Contains(pawnId) ?? false;

                    // Define the rectangle for the current pawn's row
                    Rect rowRect = new Rect(0f, currentY, scrollViewViewRect.width, 30f); // Height of the checkbox row

                    bool checkboxState = isExcluded;
                    // CheckboxLabeled puede manejar TaggedString directamente
                    Widgets.CheckboxLabeled(rowRect, pawn.LabelCap, ref checkboxState);

                    if (checkboxState != isExcluded)
                    {
                        if (checkboxState)
                        {
                            if (settings.excludedPawnIDs == null)
                            {
                                settings.excludedPawnIDs = new List<string>();
                            }
                            if (!settings.excludedPawnIDs.Contains(pawnId))
                            {
                                settings.excludedPawnIDs.Add(pawnId);
                            }
                        }
                        else
                        {
                            settings.excludedPawnIDs?.Remove(pawnId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing exclusion row for pawn '{pawn?.ThingID ?? "NULL"}': {ex}");
                }
                // --- End Exception Handling ---

                // Increment the vertical position for the next row
                currentY += rowHeight;
            }

            Widgets.EndScrollView(); // End the scroll view
            listing.End(); // End the main listing
        }
    }
}
