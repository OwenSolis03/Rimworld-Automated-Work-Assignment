using System;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// A RimWorld <see cref="GameComponent"/> that manages the timing of automatic work assignments
    /// for the Automated Work Assignment mod. It uses the <see cref="GameComponentTick"/> method
    /// to check for the start of a new in-game day. If a new day has begun and the daily refresh
    /// feature is enabled in the mod's save-specific settings (stored in <see cref="AutomatedWork_SaveData"/>),
    /// it triggers the <see cref="WorkAssigner.RefreshAssignments"/> method.
    /// It is also responsible for ensuring the ExpertModeRuleManager component is added to the game.
    /// </summary>
    public class AutoAssign_GameComponent : GameComponent
    {
        /// <summary>
        /// Tracks the RimWorld day number (as returned by <see cref="GenDate.DaysPassed"/>)
        /// when the last daily work assignment refresh check was executed. This prevents
        /// multiple checks or refreshes within the same day. Initialized to -1 to ensure
        /// the check runs on the first day loaded or started. Persisted via <see cref="ExposeData"/>.
        /// </summary>
        private int lastCheckDay = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoAssign_GameComponent"/>.
        /// This constructor ensures that the ExpertModeRuleManager is also present in the game's components.
        /// </summary>
        /// <param name="game">The current <see cref="Verse.Game"/> instance this component belongs to.</param>
        public AutoAssign_GameComponent(Game game) 
        {
            if (game.GetComponent<ExpertModeRuleManager>() == null)
            {
                game.components.Add(new ExpertModeRuleManager(game));
            }
        }

        /// <summary>
        /// Executed by the RimWorld game engine on every game tick. This method checks approximately
        /// once per in-game hour (<see cref="GenDate.TicksPerHour"/>) if the current game day
        /// (<see cref="GenDate.DaysPassed"/>) is later than the <see cref="lastCheckDay"/>.
        /// If it is a new day, it updates <see cref="lastCheckDay"/>. It then retrieves the
        /// <see cref="AutomatedWork_SaveData"/> for the current game. If the save data exists
        /// and both the mod (<c>modEnabled</c>) and the daily refresh feature (<c>enableDailyRefresh</c>)
        /// are enabled within that data, it calls <see cref="WorkAssigner.RefreshAssignments()"/>
        /// to update pawn work priorities. Includes basic error logging if the refresh fails.
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Basic null checks for safety
            if (Current.Game == null || Find.TickManager == null) return;

            // Perform the check only once per in-game hour for performance reasons
            if (Find.TickManager.TicksGame % GenDate.TicksPerHour == 0)
            {
                int currentDay = GenDate.DaysPassed;
                // Check if a new day has started since the last check
                if (currentDay > lastCheckDay)
                {
                    // Update the last check day to the current day
                    lastCheckDay = currentDay;

                    // Retrieve the per-save settings component
                    var saveData = Current.Game.GetComponent<AutomatedWork_SaveData>();

                    // Proceed only if settings exist and the relevant features are enabled
                    if (saveData != null && saveData.modEnabled && saveData.enableDailyRefresh)
                    {
                        Log.Message($"[AutoWork] Performing automatic daily check on day {currentDay}...");
                        try
                        {
                            // Trigger the core logic to reassign work priorities
                            WorkAssigner.RefreshAssignments();
                        }
                        catch (Exception ex)
                        {
                            // Log any errors that occur during the assignment process
                            Log.Error($"[AutoWork] Exception during automatic daily refresh: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Part of RimWorld's save/load system. Called by the game engine when saving or loading
        /// the game state. This method saves and loads the value of the <see cref="lastCheckDay"/> field,
        /// ensuring the component correctly remembers when the last daily check occurred across
        /// save/load cycles. Uses <see cref="Scribe_Values.Look{T}(ref T, string, T, bool)"/> for persistence.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Persist the lastCheckDay field using the standard RimWorld Scribe system
            Scribe_Values.Look(ref lastCheckDay, "lastCheckDay", -1);
        }
    }
}