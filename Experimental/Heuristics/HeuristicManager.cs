using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Automated_Work_Assignment.Experimental.Heuristics
{
    /// <summary>
    /// Central manager for the Experimental Heuristics Module.
    /// Handles recording of player actions and applying learned preferences.
    /// </summary>
    public class HeuristicManager : GameComponent
    {
        private Dictionary<string, PlayerProfile> profilesByBiome = new Dictionary<string, PlayerProfile>();

        public HeuristicManager(Game game) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref profilesByBiome, "profilesByBiome", LookMode.Value, LookMode.Deep);
            if (profilesByBiome == null)
            {
                profilesByBiome = new Dictionary<string, PlayerProfile>();
            }
        }

        /// <summary>
        /// Gets or creates a profile for the current map's biome.
        /// </summary>
        public PlayerProfile GetCurrentProfile(Map map)
        {
            if (map == null) return null;
            
            string biomeName = map.Biome.defName;
            if (!profilesByBiome.ContainsKey(biomeName))
            {
                Tile tile = Find.WorldGrid[map.Tile];
                profilesByBiome[biomeName] = new PlayerProfile(map.Biome, tile);
            }
            return profilesByBiome[biomeName];
        }

        /// <summary>
        /// Called when the player manually changes a priority in the Work tab.
        /// </summary>
        public void OnPlayerChangedPriority(Map map, Pawn pawn, WorkTypeDef workType, int newPriority)
        {
            if (map == null || workType == null) return;

            PlayerProfile profile = GetCurrentProfile(map);
            if (profile != null)
            {
                ContextSnapshot snapshot = ContextSnapshot.Capture(map);
                profile.RecordEvent(snapshot, workType.defName, newPriority);
                
                // For debugging in experimental mode
                Log.Message($"[Heuristics] Recorded manual priority change for {workType.defName} to {newPriority} in biome {profile.biomeDefName}.");
            }
        }

        /// <summary>
        /// Called during the automated work assignment refresh to apply learned weights.
        /// </summary>
        public void ApplyHeuristics(Map map)
        {
            // Future implementation:
            // 1. Get current ContextSnapshot
            // 2. Find similar past LearningEvents in the PlayerProfile
            // 3. Adjust priorities or suitability scores based on past player actions in similar contexts
            
            PlayerProfile profile = GetCurrentProfile(map);
            if (profile != null && profile.learningEvents.Count > 0)
            {
                // Skeleton: Here we would modify the WorkSettingValues or individual pawn assignments
                // Log.Message($"[Heuristics] Applying heuristics based on {profile.learningEvents.Count} past events in {profile.biomeDefName}.");
            }
        }
    }
}
