using System;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// GameComponent responsible for triggering the daily automatic work assignment refresh.
    /// Checks settings stored in the AutomatedWork_SaveData component.
    /// </summary>
    public class AutoAssign_GameComponent : GameComponent
    {
        /// <summary>
        /// Stores the day number of the last time the daily check was performed.
        /// </summary>
        private int lastCheckDay = -1;

        /// <summary>
        /// Required constructor for GameComponents.
        /// </summary>
        /// <param name="game">The current game instance.</param>
        public AutoAssign_GameComponent(Game game) { }

        /// <summary>
        /// Called periodically by the game engine.
        /// Checks if a new day has started and triggers the refresh based on per-save settings.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Current.Game == null || Find.TickManager == null) return;

            // Check once per in-game hour
            if (Find.TickManager.TicksGame % GenDate.TicksPerHour == 0)
            {
                int currentDay = GenDate.DaysPassed;
                if (currentDay > lastCheckDay)
                {
                    lastCheckDay = currentDay;

                    var saveData = Current.Game.GetComponent<AutomatedWork_SaveData>();

                    if (saveData != null && saveData.modEnabled && saveData.enableDailyRefresh)
                    {
                        Log.Message($"[AutoWork] Performing automatic daily check on day {currentDay}...");
                        try
                        {
                            WorkAssigner.RefreshAssignments();
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[AutoWork] Exception during automatic daily refresh: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles saving and loading the component's state (the lastCheckDay).
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Only need to save/load the state specific to this component
            Scribe_Values.Look(ref lastCheckDay, "lastCheckDay", -1);
        }
    }
}