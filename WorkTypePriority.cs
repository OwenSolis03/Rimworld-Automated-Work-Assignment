using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Static utility class that defines the "weight" or "cost" of different job types.
    /// Used to calculate workload penalties, preventing pawns from being assigned too many heavy tasks simultaneously.
    /// </summary>
    public static class WorkTypePriority
    {
        /// <summary>
        /// A dictionary mapping standard WorkTypeDefs to a workload integer score (1-5).
        /// Higher scores indicate more demanding or time-consuming jobs.
        /// </summary>
        public static readonly Dictionary<WorkTypeDef, int> WorkloadScore = new Dictionary<WorkTypeDef, int>
        {
            // Critical jobs (Score 5) - High impact tasks usually requiring high skill or attention
            { WorkTypeDefOf.Doctor, 5 },
            { WorkTypeDefOf.Warden, 5 },
            
            // Important jobs (Score 4) - Requires significant dedication
            { WorkTypeDefOf.Crafting, 4 },
            
            // Moderate jobs (Score 3) - Long duration projects
            { WorkTypeDefOf.Construction, 3 },
            { WorkTypeDefOf.Growing, 3 },
            { WorkTypeDefOf.Mining, 3 },
            { WorkTypeDefOf.Hunting, 3 },
            
            // Light jobs (Score 2) - Can be paused easily
            { WorkTypeDefOf.Research, 2 },
            { WorkTypeDefOf.Smithing, 2 },
            
            // Basic jobs (Score 1) - Filler work
            { WorkTypeDefOf.Cleaning, 1 },
            { WorkTypeDefOf.Hauling, 1 },
            { WorkTypeDefOf.PlantCutting, 1 }
        };

        /// <summary>
        /// Retrieves the workload score for a specific work type.
        /// </summary>
        /// <param name="workType">The work type to evaluate.</param>
        /// <returns>The workload score (1-5). Returns a default of 2 for modded/unknown work types.</returns>
        public static int GetWorkloadScore(WorkTypeDef workType)
        {
            if (WorkloadScore.TryGetValue(workType, out int score))
            {
                return score;
            }
            
            // Default value for modded work types not explicitly defined
            return 2; 
        }

        /// <summary>
        /// Calculates the total workload score for a pawn based on their currently active priorities.
        /// Iterates through all work types where the pawn has a priority > 0.
        /// </summary>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <returns>The sum of workload scores for all assigned jobs.</returns>
        public static int CalculateCurrentWorkload(Pawn pawn)
        {
            if (pawn?.workSettings == null) return 0;

            int totalLoad = 0;
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (pawn.workSettings.GetPriority(workType) > 0)
                {
                    totalLoad += GetWorkloadScore(workType);
                }
            }
            
            return totalLoad;
        }

        /// <summary>
        /// Calculates a suitability multiplier based on the pawn's current workload.
        /// If the pawn is overloaded, returns a value less than 1.0 to penalize further assignments.
        /// </summary>
        /// <param name="pawn">The pawn to check.</param>
        /// <param name="optimalWorkload">The ideal maximum workload score before penalties apply.</param>
        /// <returns>A float multiplier (0.1 to 1.0).</returns>
        public static float CalculateWorkloadMultiplier(Pawn pawn, int optimalWorkload = 8)
        {
            int currentLoad = CalculateCurrentWorkload(pawn);
            
            if (currentLoad <= optimalWorkload)
            {
                return 1.0f; // No penalty
            }
            
            // Gradual penalty calculation for overload
            int overload = currentLoad - optimalWorkload;
            float penalty = overload * 0.1f; // 10% penalty per point of overload
            
            return Math.Max(0.1f, 1.0f - penalty); // Minimum 10% effectiveness
        }
    }
}