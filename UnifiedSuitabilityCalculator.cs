using RimWorld;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Provides a unified set of methods for calculating pawn suitability for specific work types.
    /// This calculator integrates base skill metrics, passion, current workload penalties, 
    /// and continuity bonuses to determine the optimal work assignments and ensure stability.
    /// </summary>
    public static class UnifiedSuitabilityCalculator
    {
        /// <summary>
        /// Calculates the comprehensive suitability score for a pawn performing a specific work type.
        /// This final score factors in the base suitability (skill + passion), a penalty for existing workload,
        /// and a bonus for maintaining current assignments (continuity).
        /// </summary>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <param name="workType">The work type definition to check against.</param>
        /// <returns>A float representing the final suitability score; higher values indicate better suitability.</returns>
        public static float GetFinalPawnSuitability(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || workType == null || pawn.workSettings == null)
                return 0f;

            // 1. Base Score (Skill + Passion)
            float baseScore = CalculateBaseSuitability(pawn, workType);
            if (baseScore <= 0) return 0f;

            // 2. Workload Penalty
            float workloadMultiplier = WorkTypePriority.CalculateWorkloadMultiplier(pawn);

            // 3. Continuity Bonus
            float continuityMultiplier = GetContinuityMultiplier(pawn, workType);

            return baseScore * workloadMultiplier * continuityMultiplier;
        }

        /// <summary>
        /// Calculates the raw suitability score based on the pawn's relevant skill levels and passions.
        /// </summary>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <param name="workType">The work type to check.</param>
        /// <returns>The calculated base score.</returns>
        private static float CalculateBaseSuitability(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn.WorkTagIsDisabled(workType.workTags))
                return 0f;

            float score = 0f;
            
            // Aggregate score from all relevant skills
            foreach (var skillDef in workType.relevantSkills)
            {
                var skill = pawn.skills.GetSkill(skillDef);
                if (skill != null)
                {
                    score += skill.Level;
                    
                    // Apply bonus based on passion level
                    switch (skill.passion)
                    {
                        case Passion.Minor:
                            score += 2f;
                            break;
                        case Passion.Major:
                            score += 4f;
                            break;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Calculates a multiplier that favors maintaining current assignments.
        /// This helps prevent excessive job switching when scores are very close.
        /// </summary>
        /// <param name="pawn">The pawn to check.</param>
        /// <param name="workType">The work type in question.</param>
        /// <returns>A multiplier greater than 1.0 if the pawn is already assigned; otherwise 1.0.</returns>
        private static float GetContinuityMultiplier(Pawn pawn, WorkTypeDef workType)
        {
            // If the pawn is already assigned to this work, apply a bonus
            if (pawn.workSettings.GetPriority(workType) > 0)
            {
                // Critical jobs receive a higher continuity bonus to ensure stability
                int workPriority = WorkTypePriority.GetWorkloadScore(workType);
                return 1.0f + (workPriority * 0.01f); 
            }
            
            return 1.0f;
        }

        /// <summary>
        /// Determines whether a candidate pawn should replace the currently assigned pawn.
        /// A replacement only occurs if the candidate's score exceeds the current pawn's score
        /// by a specific improvement threshold.
        /// </summary>
        /// <param name="currentPawn">The pawn currently assigned to the work.</param>
        /// <param name="candidatePawn">The pawn being considered for assignment.</param>
        /// <param name="workType">The work type being evaluated.</param>
        /// <returns>True if the assignment should be switched to the candidate; otherwise, false.</returns>
        public static bool ShouldReplaceAssignment(Pawn currentPawn, Pawn candidatePawn, WorkTypeDef workType)
        {
            if (currentPawn == null || candidatePawn == null) return true;

            float currentScore = GetFinalPawnSuitability(currentPawn, workType);
            float candidateScore = GetFinalPawnSuitability(candidatePawn, workType);

            float improvementThreshold = GetImprovementThreshold(workType);
            return candidateScore > currentScore * (1 + improvementThreshold);
        }

        /// <summary>
        /// Retrieves the required improvement threshold based on the criticality of the work type.
        /// Critical jobs require a significantly better candidate to justify disruption.
        /// </summary>
        /// <param name="workType">The work type to check.</param>
        /// <returns>A float representing the percentage improvement required (e.g., 0.25 for 25%).</returns>
        private static float GetImprovementThreshold(WorkTypeDef workType)
        {
            int priority = WorkTypePriority.GetWorkloadScore(workType);
            return priority switch
            {
                5 => 0.25f, // Critical work requires 25% improvement
                4 => 0.20f, // Important work
                3 => 0.15f, // Moderate work
                2 => 0.10f, // Light work
                1 => 0.05f, // Basic work
                _ => 0.10f  // Default
            };
        }
    }
}