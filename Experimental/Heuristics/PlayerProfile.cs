using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Automated_Work_Assignment.Experimental.Heuristics
{
    /// <summary>
    /// Represents a single learning event where the player changed a priority in a specific context.
    /// </summary>
    public class LearningEvent : IExposable
    {
        public ContextSnapshot context;
        public string workTypeDefName;
        public int newPriority;

        public LearningEvent() { }

        public LearningEvent(ContextSnapshot context, string workTypeDefName, int newPriority)
        {
            this.context = context;
            this.workTypeDefName = workTypeDefName;
            this.newPriority = newPriority;
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref context, "context");
            Scribe_Values.Look(ref workTypeDefName, "workTypeDefName");
            Scribe_Values.Look(ref newPriority, "newPriority", 3);
        }
    }

    /// <summary>
    /// Stores the player's preferences and learned behaviors for a specific biome.
    /// </summary>
    public class PlayerProfile : IExposable
    {
        public string biomeDefName;
        public float baseAverageTemperature;
        public float baseRainfall;

        public List<LearningEvent> learningEvents = new List<LearningEvent>();

        public PlayerProfile() { }

        public PlayerProfile(BiomeDef biome, Tile tile)
        {
            this.biomeDefName = biome.defName;
            this.baseAverageTemperature = tile.temperature;
            this.baseRainfall = tile.rainfall;
        }

        public void RecordEvent(ContextSnapshot context, string workTypeDef, int priority)
        {
            learningEvents.Add(new LearningEvent(context, workTypeDef, priority));
            // Limit the size to avoid memory bloat
            if (learningEvents.Count > 500)
            {
                learningEvents.RemoveAt(0);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref biomeDefName, "biomeDefName");
            Scribe_Values.Look(ref baseAverageTemperature, "baseAverageTemperature", 0f);
            Scribe_Values.Look(ref baseRainfall, "baseRainfall", 0f);
            Scribe_Collections.Look(ref learningEvents, "learningEvents", LookMode.Deep);
            
            if (learningEvents == null)
            {
                learningEvents = new List<LearningEvent>();
            }
        }
    }
}
