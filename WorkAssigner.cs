using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Static class containing the core logic for calculating pawn suitability
    /// and assigning work priorities based on mod settings.
    /// </summary>
    public static class WorkAssigner
    {
        // --- Constants for Suitability Calculation ---
        /// <summary>
        /// Bonus score added for Major Passion (Burning). Vanilla equivalent is 1.5x learn speed.
        /// </summary>
        private const float PassionBurningBonus = 10f; // Example value, adjust as needed
        /// <summary>
        /// Bonus score added for Minor Passion (Interested). Vanilla equivalent is 1.0x learn speed (but often considered slightly better than none).
        /// </summary>
        private const float PassionInterestedBonus = 5f; // Example value, adjust as needed
        /// <summary>
        /// The default priority value (usually 0) assigned to pawns not selected for a specific priority.
        /// </summary>
        private const int DefaultPriority = 0;

        /// <summary>
        /// Helper struct to temporarily store a pawn and their calculated suitability score for sorting.
        /// </summary>
        private struct PawnSuitability { public Pawn pawn; public float score; }

        /// <summary>
        /// Main entry point for refreshing work assignments for all eligible colonists based on current settings.
        /// Called manually via button or automatically by AutoAssign_GameComponent.
        /// </summary>
        public static void RefreshAssignments()
        {
            AutomatedWorkSettings settings = null;
            try
            {
                settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>()?.GetSettings<AutomatedWorkSettings>();
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception retrieving settings in RefreshAssignments: {ex}");
                return; // Cannot proceed without settings
            }


            // Exit if settings couldn't be loaded or if the mod is disabled
            if (settings == null)
            {
                Log.ErrorOnce("[AutoWork] Settings are null in RefreshAssignments. Cannot perform assignment.", 1984774);
                return;
            }
            if (!settings.modEnabled)
            {
                // Log.Message("[AutoWork] Refresh skipped because mod is disabled in settings."); // Optional log
                return;
            }

            // Log.Message("[AutoWork] Starting work assignment refresh..."); // Optional start log

            List<WorkTypeDef> workTypesToManage = null;
            List<Pawn> colonists = null;

            try
            {
                // Get all work types that have actual work tags (i.e., are assignable)
                workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                    .ToList();

                // Get the list of colonists eligible for automatic assignment
                colonists = GetEligibleColonists(settings);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception retrieving WorkTypeDefs or Colonists: {ex}");
                return; // Cannot proceed if basic data retrieval fails
            }


            // Exit if there are no eligible colonists or the map isn't loaded
            if (Find.CurrentMap == null || colonists == null || !colonists.Any())
            {
                // Log.Warning("[AutoWork] No eligible colonists found or map not loaded. Skipping refresh."); // Optional warning
                return;
            }

            // Process each manageable work type
            foreach (WorkTypeDef workType in workTypesToManage)
            {
                // --- Exception Handling per WorkType ---
                try
                {
                    if (workType == null) continue; // Skip if null def somehow got through

                    // Get the specific settings (count, priority) for this work type
                    WorkSettingValues workSetting = settings.GetWorkSetting(workType.defName);
                    if (workSetting == null)
                    {
                        Log.ErrorOnce($"[AutoWork] GetWorkSetting returned null for {workType.defName} in RefreshAssignments loop!", workType.defName.GetHashCode() ^ 1);
                        continue;
                    }

                    int desiredCount = workSetting.count;
                    int targetPriority = workSetting.priority;

                    // If count is > 0, assign priorities based on suitability
                    if (desiredCount > 0)
                    {
                        AssignWorkPriorities(workType, desiredCount, targetPriority, colonists);
                    }
                    // If count is 0, ensure all eligible colonists have priority 0 for this work type
                    else
                    {
                        foreach (Pawn pawn in colonists)
                        {
                            // Safely access workSettings and set priority
                            pawn?.workSettings?.SetPriority(workType, DefaultPriority);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing WorkTypeDef '{workType?.defName ?? "NULL"}' in RefreshAssignments: {ex}");
                    // Continue to the next work type if one fails
                }
                // --- End Exception Handling per WorkType ---
            }

            // Optional: Verify the 'Work' MainButtonDef exists (useful for debugging UI issues)
            try
            {
                if (DefDatabase<MainButtonDef>.GetNamed("Work", false) == null)
                {
                    Log.ErrorOnce("[AutoWork] Could not find MainButtonDef named 'Work' in DefDatabase.", 918273645);
                }
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception checking for Work MainButtonDef: {ex}"); }


            // Log.Message("[AutoWork] Work assignment refresh complete."); // Optional end log
        }

        /// <summary>
        /// Gets a list of colonists who are eligible for automatic work assignment.
        /// Filters out downed, mentally broken, excluded, and non-colonist pawns.
        /// </summary>
        /// <param name="settings">The current mod settings, used to access the exclusion list.</param>
        /// <returns>A list of eligible Pawn objects.</returns>
        private static List<Pawn> GetEligibleColonists(AutomatedWorkSettings settings)
        {
            // Get the list of excluded pawn IDs safely (default to empty list if null)
            List<string> excludedIDs = settings?.excludedPawnIDs ?? new List<string>();

            // Return empty list if the current map is null
            if (Find.CurrentMap == null) return new List<Pawn>();

            // Query and filter pawns on the current map
            return Find.CurrentMap.mapPawns.FreeColonists // Start with free colonists
                .Where(p => p != null                     // Ensure pawn object exists
                            && p.Spawned                  // Pawn is present on the map
                            && !p.Downed                  // Pawn is not downed
                            && p.MentalStateDef == null   // Pawn is not having a mental break
                            && p.workSettings != null     // Pawn has work settings capability
                            && !excludedIDs.Contains(p.ThingID)) // Pawn is not manually excluded
                .ToList(); // Convert the result to a List
        }


        /// <summary>
        /// Calculates a numerical suitability score for a given pawn and work type.
        /// Score is primarily based on skill level and passion. Handles VSE passion compatibility.
        /// Returns -1f if the pawn is incapable of the work type.
        /// </summary>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <param name="workType">The WorkTypeDef to evaluate against.</param>
        /// <returns>A float score representing suitability, or -1f if incapable.</returns>
        private static float CalculateSuitability(Pawn pawn, WorkTypeDef workType)
        {
            try // Add general try-catch for safety when accessing pawn data
            {
                // --- Initial Capability Checks ---
                // Basic null checks and check if the pawn is fundamentally incapable of this work type
                if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType))
                {
                    return -1f; // Return -1 to indicate incapability
                }
                // --------------------------------

                // --- Placeholder for Alpha Skills compatibility ---
                // TODO: Add logic here if Alpha Skills integration is desired
                // -------------------------------------------------

                float score = 0f;
                SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault(); // Get the primary relevant skill
                SkillRecord skill = null;

                // --- Base Score from Skill Level ---
                if (relevantSkillDef != null)
                {
                    skill = pawn.skills.GetSkill(relevantSkillDef);
                    if (skill != null)
                    {
                        score += skill.Level; // Add skill level to score
                    }
                    else
                    {
                        // Pawn doesn't have the skill record? Assign a minimal base score.
                        score = 1f;
                    }
                }
                else
                {
                    // Work type has no relevant skill (e.g., Hauling, Cleaning). Assign a minimal base score.
                    score = 1f;
                }
                // ----------------------------------

                // --- Passion Bonus Calculation ---
                float passionBonus = 0f;
                if (skill != null) // Only apply passion bonus if there's a relevant skill record
                {
                    Passion passionValue = skill.passion;

                    // --- VSE Compatibility Logic ---
                    if (ModDetector.VSEIsActive)
                    {
                        ModDetector.EnsureReflectionInitialized(); // Ensure reflection attempted

                        // Proceed only if VSE reflection was successful
                        if (ModDetector.VSEReflectionSuccess && ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorProperty != null)
                        {
                            try // Specific try-catch for reflection invocation
                            {
                                // Invoke VSE method to get PassionDef from vanilla Passion enum
                                object passionDefObj = ModDetector.VSE_PassionToDefMethod.Invoke(null, new object[] { passionValue });

                                if (passionDefObj != null)
                                {
                                    // Get the learnRateFactor property value from the PassionDef object
                                    object learnFactorObj = ModDetector.VSE_LearnRateFactorProperty.GetValue(passionDefObj);

                                    // Check if the value is a float and calculate bonus
                                    if (learnFactorObj is float learnRateFactor)
                                    {
                                        // Example calculation: Scale bonus based on learn rate factor
                                        // (1.0x -> 0 bonus, 1.5x -> 5 bonus, 2.0x -> 10 bonus)
                                        float calculatedBonus = (learnRateFactor - 1.0f) * 10f;
                                        passionBonus = Mathf.Max(0f, calculatedBonus); // Ensure bonus isn't negative
#if DEBUG
                                        // Log detailed VSE reflection info only in DEBUG builds
                                        // Log.Message($"[AutoWork VSE-Reflect] P:{pawn.LabelShort} S:{skill.def.defName} V:{passionValue} Def:{passionDefObj.GetType().Name} LF:{learnRateFactor:F2} -> Bonus:{passionBonus:F1}");
#endif
                                    }
#if DEBUG
                                    else {
                                         // Log warning if learnRateFactor wasn't a float
                                         // Log.Warning($"[AutoWork VSE-Reflect] Could not get learnRateFactor as float for {pawn.LabelShort}, skill {skill.def.defName}. Type was {learnFactorObj?.GetType().Name ?? "null"}.");
                                    }
#endif
                                }
#if DEBUG
                                // else { Log.Message($"[AutoWork VSE-Reflect] VSE PassionToDef returned null for {pawn.LabelShort}, skill {skill.def.defName}, passion value {passionValue}."); }
#endif
                            }
                            catch (Exception ex)
                            {
                                // Log reflection invocation errors once per pawn/skill combo
                                Log.ErrorOnce($"[AutoWork Compat] Exception during VSE reflection invoke/get for {pawn.LabelShortCap}, skill {skill.def.defName}. Bonus set to 0. Error: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 2028);
                                passionBonus = 0f; // Default to 0 bonus on error
                            }
                        }
                        // If VSE is active but reflection failed, passionBonus remains 0 (no vanilla fallback applied here)
                    }
                    // --- End VSE Compatibility ---

                    // --- Vanilla Passion Logic ---
                    // Apply only if VSE is NOT active
                    if (!ModDetector.VSEIsActive)
                    {
                        passionBonus = passionValue == Passion.Major ? PassionBurningBonus : (passionValue == Passion.Minor ? PassionInterestedBonus : 0f);
                        passionBonus = Mathf.Max(0f, passionBonus); // Ensure bonus isn't negative
#if DEBUG
                        // Log vanilla bonus calculation only in DEBUG builds
                        // Log.Message($"[AutoWork DEBUG] Using vanilla passion bonus for {pawn.LabelShort}, skill {skill.def.defName}: {passionBonus}");
#endif
                    }
                    // --- End Vanilla Passion ---
                } // End if(skill != null)
                // --------------------------------

                // Add passion bonus to the score
                score += passionBonus;

                // Ensure a minimum score for capable pawns with relevant skills (prevents 0 score if level is 0 and no passion)
                if (score < 1f && relevantSkillDef != null)
                {
                    score = 1f;
                }

                return score;
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception in CalculateSuitability for pawn '{pawn?.ThingID ?? "NULL"}' and workType '{workType?.defName ?? "NULL"}': {ex}");
                return -1f; // Return incapable on unexpected error
            }
        }


        /// <summary>
        /// Assigns the target work priority for a specific WorkTypeDef to the most suitable colonists,
        /// up to the desired count. Sets priority to 0 for other eligible colonists.
        /// </summary>
        /// <param name="workType">The WorkTypeDef to assign priorities for.</param>
        /// <param name="desiredCount">The maximum number of pawns to assign the targetPriority.</param>
        /// <param name="targetPriority">The priority level (1-4) to assign to the selected pawns.</param>
        /// <param name="colonists">The list of all eligible colonists to consider.</param>
        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, List<Pawn> colonists)
        {
            if (workType == null || colonists == null) return; // Basic null checks

            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();

            // --- Calculate Suitability for all Pawns ---
            try
            {
                foreach (Pawn pawn in colonists)
                {
                    // Skip if pawn or workSettings are somehow null here
                    if (pawn?.workSettings == null) continue;

                    // Calculate suitability score (-1f if incapable)
                    float score = CalculateSuitability(pawn, workType);

                    // If capable (score >= 0), add to the list for sorting
                    if (score >= 0)
                    {
                        suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score });
                    }
                    // If incapable, ensure their priority for this work type is set to 0
                    else
                    {
                        pawn.workSettings.SetPriority(workType, DefaultPriority);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception calculating suitability list for WorkTypeDef '{workType.defName}': {ex}");
                return; // Stop processing this work type if suitability calculation fails broadly
            }
            // -----------------------------------------

            // --- Sort Pawns by Suitability ---
            try
            {
                // Sort descending (highest score first)
                suitabilityList.Sort((a, b) => b.score.CompareTo(a.score));
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception sorting suitability list for WorkTypeDef '{workType.defName}': {ex}");
                return; // Stop processing if sorting fails
            }
            // ---------------------------------

            // --- Determine Final Priority ---
            // Apply overrides: Doctor and Firefighter should always be priority 1 if assigned
            int priorityToAssign = targetPriority;
            if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter)
            {
                priorityToAssign = 1;
            }
            // Clamp priority to the valid range 1-4
            if (priorityToAssign < 1) priorityToAssign = 1;
            if (priorityToAssign > 4) priorityToAssign = 4;
            // --------------------------------

            // --- Assign Priorities ---
            try
            {
                // Use a HashSet for efficient tracking of pawns who received the target priority
                HashSet<Pawn> assignedPawns = new HashSet<Pawn>();

                // Assign the target priority to the top 'desiredCount' suitable pawns
                for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++)
                {
                    Pawn pawnToAssign = suitabilityList[i].pawn;
                    // Safely set priority (check pawn/worksettings again just in case)
                    pawnToAssign?.workSettings?.SetPriority(workType, priorityToAssign);
                    if (pawnToAssign != null)
                    {
                        assignedPawns.Add(pawnToAssign); // Track assignment
                    }
                }

                // Set priority to default (0) for all other suitable pawns who were not assigned the target priority
                foreach (var suitability in suitabilityList)
                {
                    // Check if the pawn exists and was NOT in the set assigned the target priority
                    if (suitability.pawn != null && !assignedPawns.Contains(suitability.pawn))
                    {
                        suitability.pawn.workSettings?.SetPriority(workType, DefaultPriority);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception assigning priorities for WorkTypeDef '{workType.defName}': {ex}");
                // Errors here might leave priorities in an inconsistent state for this work type
            }
            // -------------------------
        }
    }
}