using Verse;
using RimWorld;

namespace Automated_Work_Assignment
{
    public class AutoAssign_GameComponent : GameComponent
    {
        private int lastCheckDay = -1;
        
        public AutoAssign_GameComponent(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            if (Current.Game == null || Find.TickManager == null) return;
            
            if (Find.TickManager.TicksGame % 2000 == 0)
            {
                int currentDay = GenDate.DaysPassed;
                
                if (currentDay > lastCheckDay)
                {
                    lastCheckDay = currentDay;
                    
                    AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>()?.GetSettings<AutomatedWorkSettings>();

                    if (settings != null && settings.modEnabled && settings.enableDailyRefresh)
                    {
                        Log.Message($"[AutoWork] Performing automatic daily check on day {currentDay}...");
                        try
                        {
                            WorkAssigner.RefreshAssignments();
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"[AutoWork] Exception during automatic daily refresh: {ex}");
                        }
                    }
                }
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref lastCheckDay, "lastCheckDay", -1);
        }

    }
}