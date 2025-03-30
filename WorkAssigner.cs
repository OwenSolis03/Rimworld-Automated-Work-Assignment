using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment // Asegura consistencia
{
    public static class WorkAssigner
    {
        private const float PassionBurningBonus = 10f;
        private const float PassionInterestedBonus = 5f;
        private const int DefaultPriority = 0;

        private struct PawnSuitability { public Pawn pawn; public float score; }

        public static void RefreshAssignments()
        {
            AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>().GetSettings<AutomatedWorkSettings>();
            if (!settings.modEnabled) { return; }

            // Log.Message("[AutoWork] Starting work assignment refresh..."); // Log de inicio opcional eliminado por ahora

            List<WorkTypeDef> workTypesToManage = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wtd => wtd.workTags != WorkTags.None).ToList();

            List<Pawn> colonists = GetEligibleColonists(settings);
            if (Find.CurrentMap == null || !colonists.Any())
            {
                // Log.Warning("[AutoWork] No eligible colonists found or map not loaded. Skipping refresh."); // Warning opcional mantenido o eliminado
                return;
            }

            foreach (WorkTypeDef workType in workTypesToManage)
            {
                WorkSettingValues workSetting = settings.GetWorkSetting(workType.defName);
                int desiredCount = workSetting.count;
                int targetPriority = workSetting.priority;

                if (desiredCount > 0)
                {
                    // Llamar a la función limpia
                    AssignWorkPriorities(workType, desiredCount, targetPriority, colonists);
                }
                else
                {
                    foreach (Pawn pawn in colonists) {
                        if (pawn?.workSettings != null) {
                            pawn.workSettings.SetPriority(workType, 0);
                        }
                    }
                }
            }

            // Verificar Def del botón Work (solo para log de error si falla)
            MainButtonDef workButtonDef = DefDatabase<MainButtonDef>.GetNamed("Work", false);
            if (workButtonDef == null) { Log.ErrorOnce("[AutoWork] Could not find MainButtonDef named 'Work' in DefDatabase.", 918273645); }

            // Log.Message("[AutoWork] Work assignment refresh complete."); // Log final opcional eliminado por ahora
        }

        private static List<Pawn> GetEligibleColonists(AutomatedWorkSettings settings)
        {
            List<string> excludedIDs = settings?.excludedPawnIDs ?? new List<string>();
            if (Find.CurrentMap == null) return new List<Pawn>();

            return Find.CurrentMap.mapPawns.FreeColonists
                .Where(p => p != null && p.Spawned && !p.Downed && p.MentalStateDef == null && p.workSettings != null
                            && !excludedIDs.Contains(p.ThingID))
                .ToList();
        }


        private static float CalculateSuitability(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || pawn.skills == null || pawn.WorkTypeIsDisabled(workType)) return -1f;

            // --- Placeholder Alpha Skills ---

            float score = 0f;
            SkillDef relevantSkillDef = workType.relevantSkills?.FirstOrDefault();
            SkillRecord skill = null;

            if (relevantSkillDef != null) {
                skill = pawn.skills.GetSkill(relevantSkillDef);
                if (skill != null) { score += skill.Level; }
                else { score = 1f; }
            } else { score = 1f; }

            // --- Lógica de Pasión ---
            float passionBonus = 0f;
            if (skill != null)
            {
                Passion passionValue = skill.passion;

                // Intentar lógica VSE solo si está activo y la reflexión aún no ha fallado
                if (ModDetector.VSEIsActive)
                {
                    // Asegurarse de que se haya intentado inicializar la reflexión
                    ModDetector.EnsureReflectionInitialized();

                    // Proceder solo si la inicialización fue exitosa
                    if (ModDetector.VSEReflectionSuccess && ModDetector.VSE_PassionToDefMethod != null && ModDetector.VSE_LearnRateFactorProperty != null)
                    {
                        try
                        {
                            object passionDefObj = ModDetector.VSE_PassionToDefMethod.Invoke(null, new object[] { passionValue });

                            if (passionDefObj != null)
                            {
                                object learnFactorObj = ModDetector.VSE_LearnRateFactorProperty.GetValue(passionDefObj);

                                if (learnFactorObj is float learnRateFactor)
                                {
                                    float calculatedBonus = (learnRateFactor - 1.0f) * 10f;
                                    passionBonus = Mathf.Max(0f, calculatedBonus);
#if DEBUG
                                    Log.Message($"[AutoWork VSE-Reflect] P:{pawn.LabelShort} S:{skill.def.defName} V:{passionValue} Def:{passionDefObj.GetType().Name} LF:{learnRateFactor:F2} -> Bonus:{passionBonus:F1}");
#endif
                                }
                                else {
#if DEBUG
                                    Log.Warning($"[AutoWork VSE-Reflect] Could not get learnRateFactor as float for {pawn.LabelShort}, skill {skill.def.defName}. Type was {learnFactorObj?.GetType().Name ?? "null"}.");
#endif
                                }
                            }
#if DEBUG
                            else {
                                Log.Message($"[AutoWork VSE-Reflect] VSE PassionToDef returned null for {pawn.LabelShort}, skill {skill.def.defName}, passion value {passionValue}.");
                            }
#endif
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorOnce($"[AutoWork Compat] Exception during VSE reflection invoke/get for {pawn.LabelShortCap}, skill {skill.def.defName}. Bonus set to 0. Error: {ex.Message}", pawn.thingIDNumber ^ skill.def.shortHash ^ 2028);
                            passionBonus = 0f;
                        }
                    }
                    // Si VSE está activo pero la reflexión falló (VSEReflectionSuccess es false), no hacemos nada aquí,
                    // se usará la lógica vanilla en el 'else' de abajo si es necesario redefinirlo.
                    // Actualmente, si VSEIsActive es true pero VSEReflectionSuccess es false, passionBonus será 0.
                    // Podríamos añadir un fallback a vanilla aquí si quisiéramos.

                } // Fin if (ModDetector.VSEIsActive)

                // Aplicar lógica vanilla solo si VSE NO está activo
                if (!ModDetector.VSEIsActive)
                {
                    passionBonus = passionValue == Passion.Major ? PassionBurningBonus : (passionValue == Passion.Minor ? PassionInterestedBonus : 0f);
                    passionBonus = Mathf.Max(0f, passionBonus);
#if DEBUG
                    // Log.Message($"[AutoWork DEBUG] Using vanilla passion bonus for {pawn.LabelShort}, skill {skill.def.defName}: {passionBonus}");
#endif
                }
            } // Fin if(skill != null)

            score += passionBonus;

            if (score < 1f && relevantSkillDef != null) { score = 1f; }

            return score;
        }


        // Método AssignWorkPriorities SIN logs de depuración
        private static void AssignWorkPriorities(WorkTypeDef workType, int desiredCount, int targetPriority, List<Pawn> colonists)
        {
            // Log.Message($"[AutoWork Debug] AssignWorkPriorities START..."); // ELIMINADO

            List<PawnSuitability> suitabilityList = new List<PawnSuitability>();
            foreach (Pawn pawn in colonists) {
                if (pawn?.workSettings == null) continue;
                float score = CalculateSuitability(pawn, workType);
                if (score >= 0) {
                    suitabilityList.Add(new PawnSuitability { pawn = pawn, score = score });
                } else {
                    pawn.workSettings.SetPriority(workType, 0);
                }
            }

            suitabilityList.Sort((a, b) => b.score.CompareTo(a.score));

            // Log.Message($"[AutoWork Debug] Suitability list count: ..."); // ELIMINADO
            // Log.Message($"  #{k+1}: ..."); // ELIMINADO

            int priorityToAssign = targetPriority;
            if (workType == WorkTypeDefOf.Doctor || workType == WorkTypeDefOf.Firefighter) { priorityToAssign = 1; }
            if (priorityToAssign < 1) priorityToAssign = 1;
            if (priorityToAssign > 4) priorityToAssign = 4;

            // Log.Message($"[AutoWork Debug] Final priorityToAssign..."); // ELIMINADO

            HashSet<Pawn> assignedPawns = new HashSet<Pawn>();
            // Log.Message($"[AutoWork Debug] Assigning top N..."); // ELIMINADO
            for (int i = 0; i < suitabilityList.Count && i < desiredCount; i++) {
                Pawn pawnToAssign = suitabilityList[i].pawn;
                // Log.Message($"  LOOP i={i}: Assigning..."); // ELIMINADO
                pawnToAssign.workSettings.SetPriority(workType, priorityToAssign);
                assignedPawns.Add(pawnToAssign);
            }

            // Log.Message($"[AutoWork Debug] Setting default priority..."); // ELIMINADO
            foreach(var suitability in suitabilityList) {
                if(!assignedPawns.Contains(suitability.pawn)) {
                    // Log.Message($"  Setting {pawn} -> Priority 0"); // ELIMINADO
                    suitability.pawn.workSettings.SetPriority(workType, DefaultPriority);
                }
            }

            // Log.Message($"[AutoWork Debug] AssignWorkPriorities END..."); // ELIMINADO
        }
    }
}