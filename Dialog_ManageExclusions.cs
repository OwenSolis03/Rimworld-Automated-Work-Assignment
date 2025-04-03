using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

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
            forcePause = true;      // Pauses the game while open
            doCloseX = true;        // Show the 'X' close button
            closeOnClickedOutside = true; // Close if user clicks outside the window
            absorbInputAroundWindow = true; // Prevent clicks passing through to the game world
            draggable = true;       // Allow the user to drag the window

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
                                     .Where(p => p != null && !p.DevelopmentalStage.Baby()) // Ensure pawn exists and is not a baby
                                     .OrderBy(p => p.LabelCap.ToString()) // Order by name (ensure LabelCap is converted to string if needed)
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
            // Define the outer rectangle for the scroll view area
            // Use Gap() to reserve space instead of GetRect() if not adding more elements below
            listing.Gap(inRect.height - listing.CurHeight - 50f); // Reserve remaining space minus bottom margin
            Rect scrollViewOutRect = new Rect(inRect.x, listing.CurHeight - (inRect.height - listing.CurHeight - 50f), inRect.width, inRect.height - listing.CurHeight - 50f);
            // Alternative calculation based on GetRect, assuming GetRect was the last call
            // Rect scrollViewOutRect = listing.GetRect(inRect.height - listing.CurHeight - 50f); // Use appropriate height


            // Calculate the total height needed for all pawn rows
            float rowHeight = 32f; // Height per pawn row + spacing
            float viewHeight = availablePawns.Count * rowHeight;
            // Define the inner rectangle representing the total scrollable content size
            Rect scrollViewViewRect = new Rect(0f, 0f, scrollViewOutRect.width - 16f, viewHeight); // Subtract scrollbar width

            // --- Draw ScrollView ---
            Widgets.BeginScrollView(scrollViewOutRect, ref scrollPosition, scrollViewViewRect);

            float currentY = 0f; // Manual tracking of vertical position within the scroll view

            // Iterate through each available pawn
            foreach (Pawn pawn in availablePawns)
            {
                // --- Exception Handling for each pawn row ---
                try
                {
                    // Basic null check, although list should already be filtered
                    if (pawn == null) continue;

                    string pawnId = pawn.ThingID; // Get the pawn's unique ID

                    // Check if the pawn is currently in the exclusion list (handle null list safely)
                    bool isExcluded = settings.excludedPawnIDs?.Contains(pawnId) ?? false;

                    // Define the rectangle for the current pawn's row
                    Rect rowRect = new Rect(0f, currentY, scrollViewViewRect.width, 30f); // Height of the checkbox row

                    // Store the current checkbox state to detect changes
                    bool checkboxState = isExcluded;

                    // Draw the checkbox with the pawn's name
                    Widgets.CheckboxLabeled(rowRect, pawn.LabelCap.ToString(), ref checkboxState); // Ensure LabelCap is string

                    // Check if the checkbox state was changed by the user
                    if (checkboxState != isExcluded)
                    {
                        // If checked (meaning exclude the pawn)
                        if (checkboxState)
                        {
                            // Ensure the exclusion list exists before adding
                            if (settings.excludedPawnIDs == null)
                            {
                                settings.excludedPawnIDs = new List<string>();
                            }
                            // Add the pawn ID if not already present
                            if (!settings.excludedPawnIDs.Contains(pawnId))
                            {
                                settings.excludedPawnIDs.Add(pawnId);
                            }
                        }
                        // If unchecked (meaning include the pawn / remove from exclusion)
                        else
                        {
                            // Safely attempt to remove the pawn ID from the list (handles null list)
                            settings.excludedPawnIDs?.Remove(pawnId);
                        }
                        // Note: Settings are saved automatically when the ModSettings window is closed or via WriteSettings()
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing exclusion row for pawn '{pawn?.ThingID ?? "NULL"}': {ex}");
                    // Draw an error message? Difficult with manual Y positioning. Best to just log and continue.
                }
                // --- End Exception Handling ---

                // Increment the vertical position for the next row
                currentY += rowHeight; // Use the defined row height including spacing
            }

            Widgets.EndScrollView(); // End the scroll view

            listing.End(); // End the main listing
        }
    }
}