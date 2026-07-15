using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Dialog for managing which colonists are excluded from specific job types.
    /// This allows fine-grained control (e.g., "I never want this specific pawn to be a Doctor, but they can do other jobs").
    /// Interacts with the <see cref="AutomatedWork_SaveData.perJobExcludedPawnIDs"/> dictionary.
    /// </summary>
    public class Dialog_ManageJobExclusions : Window
    {
        private readonly AutomatedWork_SaveData saveData;
        private readonly WorkTypeDef selectedWorkType;
        
        /// <summary>
        /// Maintains the vertical scroll position within the pawn list view.
        /// </summary>
        private Vector2 scrollPosition = Vector2.zero;
        
        /// <summary>
        /// Cached list of pawns eligible for exclusion (free colonists on the map).
        /// </summary>
        private List<Pawn> availablePawns;

        /// <summary>
        /// Defines the initial dimensions of the window.
        /// </summary>
        public override Vector2 InitialSize => new Vector2(500f, 700f);

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialog_ManageJobExclusions"/> class.
        /// </summary>
        /// <param name="currentSaveData">The save data component containing exclusion lists.</param>
        /// <param name="workType">The specific work type (e.g., Cooking, Mining) being managed.</param>
        /// <exception cref="ArgumentNullException">Thrown if inputs are null.</exception>
        public Dialog_ManageJobExclusions(AutomatedWork_SaveData currentSaveData, WorkTypeDef workType)
        {
            saveData = currentSaveData ?? throw new ArgumentNullException(nameof(currentSaveData));
            selectedWorkType = workType ?? throw new ArgumentNullException(nameof(workType));

            forcePause = true;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;

            this.optionalTitle = $"Exclude Pawns from {workType.labelShort.CapitalizeFirst()}";
            RefreshPawnList();
        }

        /// <summary>
        /// Refreshes the list of available pawns from the current map.
        /// Filters out nulls and babies, then sorts by label.
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
                Log.Error($"[AWA] Exception refreshing pawn list for job exclusions: {ex}");
                availablePawns = new List<Pawn>();
            }
        }

        /// <summary>
        /// Draws the content of the window.
        /// Lists all eligible pawns with checkboxes to toggle their exclusion status for the selected job.
        /// Also indicates if a pawn is natively incapable of the work type.
        /// </summary>
        /// <param name="inRect">The available drawing area.</param>
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            try 
            {
#if RIMWORLD_1_6
                listing.Label($"Select pawns to exclude from {selectedWorkType.labelShort}:");
                listing.Label("Excluded pawns will never be assigned to this job by the mod.");
                listing.Label("(They can still be manually assigned in the Work tab)");
#elif RIMWORLD_1_5
                string t1 = $"Select pawns to exclude from {selectedWorkType.labelShort}:";
                Widgets.Label(listing.GetRect(Text.CalcHeight(t1, listing.ColumnWidth)), t1);
                listing.Gap(listing.verticalSpacing);
                
                string t2 = "Excluded pawns will never be assigned to this job by the mod.";
                Widgets.Label(listing.GetRect(Text.CalcHeight(t2, listing.ColumnWidth)), t2);
                listing.Gap(listing.verticalSpacing);
                
                string t3 = "(They can still be manually assigned in the Work tab)";
                Widgets.Label(listing.GetRect(Text.CalcHeight(t3, listing.ColumnWidth)), t3);
                listing.Gap(listing.verticalSpacing);
#endif
                listing.GapLine();
            } 
            catch (Exception ex) 
            { 
                Log.Error($"[AWA] Exception drawing job exclusion header: {ex}"); 
            }

            if (availablePawns == null) 
            {
#if RIMWORLD_1_6
                listing.Label("Error: Could not load pawn list.");
#elif RIMWORLD_1_5
                string t4 = "Error: Could not load pawn list.";
                Widgets.Label(listing.GetRect(Text.CalcHeight(t4, listing.ColumnWidth)), t4);
                listing.Gap(listing.verticalSpacing);
#endif
                listing.End();
                return;
            }

            // Scroll view layout calculations
            const float rowHeight = 32f;
            float currentYPos = listing.CurHeight;
            float availableHeight = inRect.height - currentYPos - 50f;
            float scrollViewHeight = Mathf.Max(100f, availableHeight);
            Rect scrollViewOutRect = new Rect(inRect.x, currentYPos, inRect.width, scrollViewHeight);

            float viewHeight = availablePawns.Count * rowHeight;
            float viewWidth = Mathf.Max(0f, scrollViewOutRect.width - 16f);
            Rect scrollViewViewRect = new Rect(0f, 0f, viewWidth, viewHeight);

            Widgets.BeginScrollView(scrollViewOutRect, ref scrollPosition, scrollViewViewRect);
            float currentY = 0f;

            string workDefName = selectedWorkType.defName;
            
            // Ensure the dictionary entry exists for this work type before accessing it
            if (!saveData.perJobExcludedPawnIDs.ContainsKey(workDefName))
            {
                saveData.perJobExcludedPawnIDs[workDefName] = new List<string>();
            }

            List<string> excludedForThisJob = saveData.perJobExcludedPawnIDs[workDefName];

            foreach (Pawn pawn in availablePawns)
            {
                try
                {
                    if (pawn == null) continue;
                    string pawnId = pawn.ThingID;

                    bool isExcluded = excludedForThisJob.Contains(pawnId);
                    Rect rowRect = new Rect(0f, currentY, scrollViewViewRect.width, 30f);
                    bool checkboxState = isExcluded;

                    // Visual indicator if pawn is incapable of this work (e.g. traits or backstory)
                    string label = pawn.LabelCap;
                    Color originalColor = GUI.color;
                    
                    if (pawn.WorkTypeIsDisabled(selectedWorkType))
                    {
                        label += " (Incapable)";
                        GUI.color = Color.gray; // Dim the text to indicate incapability
                    }

                    Widgets.CheckboxLabeled(rowRect, label, ref checkboxState);
                    GUI.color = originalColor; // Restore color

                    // Update exclusion list if checkbox state changed
                    if (checkboxState != isExcluded)
                    {
                        if (checkboxState)
                        {
                            if (!excludedForThisJob.Contains(pawnId))
                                excludedForThisJob.Add(pawnId);
                        }
                        else
                        {
                            excludedForThisJob.Remove(pawnId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AWA] Exception processing job exclusion row for pawn '{pawn?.ThingID ?? "NULL"}': {ex}");
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