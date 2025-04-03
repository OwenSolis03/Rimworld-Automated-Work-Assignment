using System;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// GameComponent responsible for triggering the daily automatic work assignment refresh.
    /// </summary>
    public class AutoAssign_GameComponent : GameComponent
    {
        /// <summary>
        /// Stores the day number of the last time the daily check was performed.
        /// Used to ensure the refresh runs only once per day. Initialized to -1.
        /// </summary>
        private int lastCheckDay = -1;

        /// <summary>
        /// Constructor for the GameComponent. Required by RimWorld.
        /// </summary>
        /// <param name="game">The current game instance.</param>
        public AutoAssign_GameComponent(Game game) { } // Base constructor is sufficient

        /// <summary>
        /// Called periodically by the game engine (roughly every tick).
        /// Contains the logic to check if a new day has started and trigger the refresh if enabled.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Basic null checks for safety, especially during game load/save
            if (Current.Game == null || Find.TickManager == null) return;

            // --- Optimization: Check less frequently ---
            // Check once per in-game hour (2500 ticks) instead of every 2000 ticks.
            // The core logic only needs to run once per day anyway.
            if (Find.TickManager.TicksGame % GenDate.TicksPerHour == 0)
            {
                int currentDay = GenDate.DaysPassed;

                // Check if the current day is later than the last recorded check day
                if (currentDay > lastCheckDay)
                {
                    // Update the last check day to prevent multiple checks on the same day
                    lastCheckDay = currentDay;

                    // Retrieve mod settings safely
                    AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>()?.GetSettings<AutomatedWorkSettings>();

                    // Proceed only if settings are loaded, the mod is enabled, and daily refresh is enabled
                    if (settings != null && settings.modEnabled && settings.enableDailyRefresh)
                    {
                        // Log message indicating the automatic action is triggered
                        Log.Message($"[AutoWork] Performing automatic daily check on day {currentDay}...");

                        // --- Exception Handling for the core action ---
                        try
                        {
                            // Call the static method to refresh work assignments
                            WorkAssigner.RefreshAssignments();
                        }
                        catch (Exception ex)
                        {
                            // Log any errors that occur during the refresh process
                            Log.Error($"[AutoWork] Exception during automatic daily refresh: {ex}");
                        }
                        // --- End Exception Handling ---
                    }
                }
            }
        }

        /// <summary>
        /// Handles saving and loading the component's state (the lastCheckDay).
        /// Called by RimWorld during game save/load operations.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Save/Load the lastCheckDay value. Defaults to -1 if not found during load.
            Scribe_Values.Look(ref lastCheckDay, "lastCheckDay", -1);
        }
    }
}