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
        /// <summary>
        /// Bonus score added for Major Passion (Burning).
        /// </summary>
        private const float PassionBurningBonus = 10f;
        /// <summary>
        /// Bonus score added for Minor Passion (Interested).
        /// </summary>
        private const float PassionInterestedBonus = 5f;
        /// <summary>
        /// The default priority value (0) assigned to pawns not selected for a specific priority.
        /// </summary>
        private const int DefaultPriority = 0;

        /// <summary>
        /// Helper struct to temporarily store a pawn and their calculated suitability score for sorting.
        /// </summary>
        private struct PawnSuitability { public Pawn pawn; public float score; }

        /// <summary>
        /// Main entry point for refreshing work assignments for all eligible colonists based on current settings.
        /// Called manually via button or automatically by AutoAssign_GameComponent.
        /// Skips processing for any WorkTypeDefs marked as excluded in the mod settings.
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
                return;
            }

            if (settings == null)
            {
                Log.ErrorOnce("[AutoWork] Settings are null in RefreshAssignments. Cannot perform assignment.", 1984774);
                return;
            }
            if (!settings.modEnabled)
            {
                return;
            }

            List<WorkTypeDef> workTypesToManage = null;
            List<Pawn> colonists = null;

            try
            {
                workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Where(wtd => wtd != null && wtd.workTags != WorkTags.None)
                    .ToList();
                colonists = GetEligibleColonists(settings);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception retrieving WorkTypeDefs or Colonists: {ex}");
                return;
            }

            if (Find.CurrentMap == null || colonists == null || !colonists.Any())
            {
                return;
            }

            // Process each manageable work type
            foreach (WorkTypeDef workType in workTypesToManage)
            {
                try
                {
                    if (workType == null) continue;

                    // Check if this WorkTypeDef is excluded in settings
                    if (settings.excludedWorkTypeDefNames != null && settings.excludedWorkTypeDefNames.Contains(workType.defName))
                    {
                        continue; // Skip this work type if it's excluded
                    }

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
                            pawn?.workSettings?.SetPriority(workType, DefaultPriority);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutoWork] Exception processing WorkTypeDef '{workType?.defName ?? "NULL"}' in RefreshAssignments: {ex}");
                }
            } // End foreach WorkTypeDef

            // Optional check for MainButtonDef
            try
            {
                if (DefDatabase<MainButtonDef>.GetNamed("Work", false) == null)
                {
                    Log.ErrorOnce("[AutoWork] Could not find MainButtonDef named 'Work' in DefDatabase.", 918273645);
                }
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception checking for Work MainButtonDef: {ex}"); }

        } // End RefreshAssignments

        /// <summary>
        /// Gets a list of colonists who are eligible for automatic work assignment.
        /// Filters out downed, mentally broken, excluded (by pawn ID), and non-colonist pawns.
        /// </summary>
        /// <param name="settings">The current mod settings, used to access the pawn exclusion list.</param>
        /// <returns>A list of eligible Pawn objects.</returns>
        private static List<Pawn> GetEligibleColonists(AutomatedWorkSettings settings)
        {
            List<string> excludedIDs = settings?.excludedPawnIDs ?? new List<string>();
            if (Find.CurrentMap == null) return new List<Pawn>();

            return Find.CurrentMap.mapPawns.FreeColonists
                .Where(p => p != null
                            && p.Spawned
                            && !p.Downed
                            && p.MentalStateDef == null
                            && p.workSettings != null
                            && !excludedIDs.Contains(p.ThingID))
                .ToList();
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
            try
            {
                if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) return -1f;

                // --- Placeholder for Alpha Skills compatibility ---

                float score = 0f;
                SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
                SkillRecord skill = null;

                if (relevantSkillDef != null)
                {
                    skill = pawn.skills.GetSkill(relevantSkillDef);
                    score = skill != null ? skill.Level : 1f;
                } else { score = 1f; }

                float passionBonus = 0f;
                if (skill != null)
                {
                    Passion passionValue = skill.passion;
                    if (ModDetector.VSEIsActive)
                    {
                        ModDetector.EnsureReflectionInitialized();
                        if (ModDetector.VSEReflectionSuccess && ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorField != null)
                        {
                            try
                            {
                                object passionDefObj = ModDetector.VSE_PassionToDefMethod.Invoke(null, new object[] { passionValue });
                                if (passionDefObj != null)
                                {
                                    object learnFactorObj = ModDetector.VSE_LearnRateFactorField.GetValue(passionDefObj);
                                    if (learnFactorObj is float learnRateFactor)
                                    {
                                        passionBonus = Mathf.Max(0f, (learnRateFactor - 1.0f) * 10f);
#if DEBUG
                                        // Log.Message($"[AutoWork VSE-Reflect] P:{pawn.LabelShort} S:{skill.def.defName} V:{passionValue} Def:{passionDefObj.GetType().Name} LF:{learnRateFactor:F2} -> Bonus:{passionBonus:F1}");
#endif
                                    }
#if DEBUG
                                    // else { Log.Warning($"[AutoWork VSE-Reflect] Could not get learnRateFactor as float..."); }
#endif
                                }
#if DEBUG
                                // else { Log.Message($"[AutoWork VSE-Reflect] VSE PassionToDef returned null..."); }
#endif
                            }
                            catch (Exception ex)
                            {
                                Log.ErrorOnce($"[AutoWork Compat] Exception during VSE reflection invoke/get for {pawn.LabelShortCap}, skill {skill.def.defName}. Bonus set to 0. Error: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 2028);
                                passionBonus = 0f;
                            }
                        }
                    }

                    // Use vanilla bonus if VSE not active OR reflection failed
                    if (!ModDetector.VSEIsActive || !ModDetector.VSEReflectionSuccess)
                    {
                        passionBonus = passionValue == Passion.Major ? PassionBurningBonus : (passionValue == Passion.Minor ? PassionInterestedBonus : 0f);
                        passionBonus = Mathf.Max(0f, passionBonus);
#if DEBUG
                        // Log vanilla bonus calculation
#endif
                    }
                }
                score += passionBonus;
                if (score < 1f && relevantSkillDef != null) score = 1f;
                return score;
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception in CalculateSuitability for pawn '{pawn?.ThingID ?? "NULL"}' and workType '{workType?.defName ?? "NULL"}': {ex}");
                return -1f;
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
            if (workType == null || colonists == null) return;

            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();

            try
            {
                foreach (Pawn pawn in colonists)
                {
                    if (pawn?.workSettings == null) continue;
                    float score = CalculateSuitability(pawn, workType);
                    if (score >= 0)
                    {
                        suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score });
                    }
                    else
                    {
                        pawn.workSettings.SetPriority(workType, DefaultPriority);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception calculating suitability list for WorkTypeDef '{workType.defName}': {ex}"); return; }

            try { suitabilityList.Sort((a, b) => b.score.CompareTo(a.score)); }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception sorting suitability list for WorkTypeDef '{workType.defName}': {ex}"); return; }

            int priorityToAssign = targetPriority;
            if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter) priorityToAssign = 1;
            if (priorityToAssign < 1) priorityToAssign = 1;
            if (priorityToAssign > 4) priorityToAssign = 4;

            try
            {
                HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
                for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++)
                {
                    Pawn pawnToAssign = suitabilityList[i].pawn;
                    pawnToAssign?.workSettings?.SetPriority(workType, priorityToAssign);
                    if (pawnToAssign != null) assignedPawns.Add(pawnToAssign);
                }

                foreach (var suitability in suitabilityList)
                {
                    if (suitability.pawn != null && !assignedPawns.Contains(suitability.pawn))
                    {
                        suitability.pawn.workSettings?.SetPriority(workType, DefaultPriority);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"[AutoWork] Exception assigning priorities for WorkTypeDef '{workType.defName}': {ex}"); }
        }

    } // End Class WorkAssigner
}