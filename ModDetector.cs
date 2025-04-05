using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
// Required for FieldInfo

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Static class responsible for detecting the presence of other mods
    /// and managing compatibility features, particularly reflection-based interaction
    /// with Vanilla Skills Expanded (VSE).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModDetector
    {
        // --- Public detection flags ---
        /// <summary>
        /// Gets a value indicating whether Vanilla Skills Expanded is currently active.
        /// </summary>
        public static bool VSEIsActive { get; private set; }
        /// <summary>
        /// Gets a value indicating whether Alpha Skills is currently active.
        /// </summary>
        public static bool AlphaSkillsIsActive { get; private set; }

        // --- VSE Reflection Info (Internal access) ---
        /// <summary>
        /// Cached MethodInfo for VSE's PassionManager.PassionToDef(Passion).
        /// </summary>
        internal static MethodInfo VSE_PassionToDefMethod { get; private set; } = null;

        /// <summary>
        /// Cached FieldInfo for VSE's PassionDef.learnRateFactor.
        /// Null if VSE is not active or reflection failed.
        /// </summary>
        internal static FieldInfo VSE_LearnRateFactorField { get; private set; } = null;

        /// <summary>
        /// Flag indicating whether the reflection attempt for VSE has already been made.
        /// </summary>
        private static bool reflectionAttempted = false;

        /// <summary>
        /// Gets a value indicating whether the reflection attempt for VSE members was successful.
        /// </summary>
        internal static bool VSEReflectionSuccess { get; private set; } = false;

        /// <summary>
        /// Static constructor. Detects active mods at startup.
        /// </summary>
        static ModDetector()
        {
            VSEIsActive = ModLister.GetActiveModWithIdentifier("vanillaexpanded.skills", true) != null;
            AlphaSkillsIsActive = ModLister.GetActiveModWithIdentifier("sarg.alphaskills", true) != null;

            Log.Message($"[AutoWork] Compatibility: Vanilla Skills Expanded {(VSEIsActive ? "DETECTED" : "NOT detected")}.");
            Log.Message($"[AutoWork] Compatibility: Alpha Skills {(AlphaSkillsIsActive ? "DETECTED" : "NOT detected")}.");
        }

        /// <summary>
        /// Ensures that the reflection process to find VSE members has been attempted exactly once.
        /// </summary>
        internal static void EnsureReflectionInitialized()
        {
            if (reflectionAttempted) return;
            reflectionAttempted = true;

            if (!VSEIsActive) return;

            try
            {
                Type passionManagerType = AccessTools.TypeByName("VSE.Passions.PassionManager");
                Type passionDefType = AccessTools.TypeByName("VSE.Passions.PassionDef");

                if (passionManagerType == null || passionDefType == null) {
                    Log.Warning("[AutoWork Compat] Could not find VSE PassionManager or PassionDef type via AccessTools. VSE compatibility features disabled.");
                    VSEReflectionSuccess = false;
                    return;
                }

                VSE_PassionToDefMethod = AccessTools.Method(passionManagerType, "PassionToDef", new Type[] { typeof(Passion) });
                VSE_LearnRateFactorField = AccessTools.Field(passionDefType, "learnRateFactor");

                if (VSE_PassionToDefMethod != null && VSE_LearnRateFactorField != null)
                {
                    VSEReflectionSuccess = true;
                    // Log.Message("[AutoWork Compat] VSE reflection info successfully obtained (Method and Field).");
                }
                else
                {
                    if (VSE_PassionToDefMethod == null) Log.Warning("[AutoWork Compat] Could not find VSE method PassionManager.PassionToDef via AccessTools.");
                    if (VSE_LearnRateFactorField == null) Log.Warning("[AutoWork Compat] Could not find VSE field PassionDef.learnRateFactor via AccessTools.");
                    VSEReflectionSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork Compat] Exception during VSE reflection initialization: {ex}");
                VSEReflectionSuccess = false;
            }
        }
    }
}