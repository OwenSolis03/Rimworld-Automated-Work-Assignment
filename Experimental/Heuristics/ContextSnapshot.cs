using System;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment.Experimental.Heuristics
{
    /// <summary>
    /// Represents the game state at the exact moment a player changes a work priority.
    /// Used by the Heuristic module to understand the context of player decisions.
    /// </summary>
    public struct ContextSnapshot : IExposable
    {
        public int dayOfYear;
        public int hourOfDay;
        public float outdoorTemperature;
        
        // Key resources
        public float totalNutrition;
        public int woodCount;
        public int steelCount;
        public int medicineCount;
        public int componentCount;

        /// <summary>
        /// Captures the current state of the map.
        /// </summary>
        public static ContextSnapshot Capture(Map map)
        {
            ContextSnapshot snapshot = new ContextSnapshot();
            
            snapshot.dayOfYear = GenDate.DayOfYear(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x);
            snapshot.hourOfDay = GenDate.HourOfDay(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x);
            snapshot.outdoorTemperature = map.mapTemperature.OutdoorTemp;

            // Gather critical resources using the efficient ResourceCounter
            snapshot.totalNutrition = map.resourceCounter.TotalHumanEdibleNutrition;
            snapshot.woodCount = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
            snapshot.steelCount = map.resourceCounter.GetCount(ThingDefOf.Steel);
            snapshot.medicineCount = map.resourceCounter.GetCount(ThingDefOf.MedicineIndustrial) + 
                                     map.resourceCounter.GetCount(ThingDefOf.MedicineHerbal) + 
                                     map.resourceCounter.GetCount(ThingDefOf.MedicineUltratech);
            snapshot.componentCount = map.resourceCounter.GetCount(ThingDefOf.ComponentIndustrial);

            return snapshot;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref dayOfYear, "dayOfYear", 0);
            Scribe_Values.Look(ref hourOfDay, "hourOfDay", 0);
            Scribe_Values.Look(ref outdoorTemperature, "outdoorTemperature", 0f);
            
            Scribe_Values.Look(ref totalNutrition, "totalNutrition", 0f);
            Scribe_Values.Look(ref woodCount, "woodCount", 0);
            Scribe_Values.Look(ref steelCount, "steelCount", 0);
            Scribe_Values.Look(ref medicineCount, "medicineCount", 0);
            Scribe_Values.Look(ref componentCount, "componentCount", 0);
        }
    }
}
