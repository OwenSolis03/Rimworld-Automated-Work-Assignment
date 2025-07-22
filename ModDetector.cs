using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
// Required for FieldInfo

namespace Automated_Work_Assignment
{
    /// <summary>
    /// A static utility class responsible for detecting the presence of specific other mods
    /// that Automated Work Assignment might interact with. It manages compatibility states
    /// and facilitates reflection-based interaction, particularly with Vanilla Skills Expanded (VSE),
    /// to access features without requiring a hard dependency.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModDetector
    {
        // --- Public detection flags ---
        /// <summary>
        /// Gets a boolean value indicating whether the 'Vanilla Skills Expanded' mod
        /// is currently loaded and active in the game. Determined at startup.
        /// </summary>
        public static bool VSEIsActive { get; private set; }
        /// <summary>
        /// Gets a boolean value indicating whether the 'Alpha Skills' mod
        /// is currently loaded and active in the game. Determined at startup.
        /// </summary>
        public static bool AlphaSkillsIsActive { get; private set; }

        // --- VSE Reflection Info (Internal access - used by other parts of this mod) ---
        /// <summary>
        /// Cached <see cref="MethodInfo"/> representing the 'PassionManager.PassionToDef(Passion)' method
        /// from Vanilla Skills Expanded. This is obtained via reflection if VSE is active.
        /// Remains null if VSE is not active or reflection fails.
        /// </summary>
        internal static MethodInfo VSE_PassionToDefMethod { get; private set; } = null;

        /// <summary>
        /// Cached <see cref="FieldInfo"/> representing the 'PassionDef.learnRateFactor' field
        /// from Vanilla Skills Expanded. This is obtained via reflection if VSE is active.
        /// Remains null if VSE is not active or reflection fails.
        /// </summary>
        internal static FieldInfo VSE_LearnRateFactorField { get; private set; } = null;

        /// <summary>
        /// A private flag ensuring the reflection process to find VSE members is only attempted once
        /// during the application's lifetime to avoid redundant lookups and potential errors.
        /// </summary>
        private static bool reflectionAttempted = false;

        /// <summary>
        /// Gets a boolean value indicating whether the reflection attempt to locate necessary
        /// Vanilla Skills Expanded members (like methods and fields) was successful.
        /// This is false if VSE is inactive or if reflection failed.
        /// </summary>
        internal static bool VSEReflectionSuccess { get; private set; } = false;

        /// <summary>
        /// Static constructor, executed automatically on game startup.
        /// Checks for the presence of Vanilla Skills Expanded and Alpha Skills mods
        /// using their unique identifiers and updates the respective boolean flags.
        /// Logs the detection status for debugging purposes.
        /// </summary>
        static ModDetector()
        {
            // Use ModLister to check if mods with specific package IDs are active.
            VSEIsActive = ModLister.GetActiveModWithIdentifier("vanillaexpanded.skills", true) != null;
            AlphaSkillsIsActive = ModLister.GetActiveModWithIdentifier("sarg.alphaskills", true) != null;

            // Log the results to the game's console.
            Log.Message($"[AutoWork] Compatibility: Vanilla Skills Expanded {(VSEIsActive ? "DETECTED" : "NOT detected")}.");
            Log.Message($"[AutoWork] Compatibility: Alpha Skills {(AlphaSkillsIsActive ? "DETECTED" : "NOT detected")}.");

            // Note: Reflection initialization is deferred until actually needed via EnsureReflectionInitialized.
        }

        /// <summary>
        /// Ensures that the reflection process to find Vanilla Skills Expanded (VSE) members
        /// (methods and fields needed for compatibility) has been attempted. This method is designed
        /// to run only once. If VSE is active, it attempts to find the required members using
        /// Harmony's AccessTools. Updates <see cref="VSEReflectionSuccess"/> based on the outcome.
        /// Logs warnings or errors if reflection fails.
        /// </summary>
        internal static void EnsureReflectionInitialized()
        {
            // Prevent multiple reflection attempts.
            if (reflectionAttempted) return;
            reflectionAttempted = true;

            // Skip reflection if VSE isn't active.
            if (!VSEIsActive) return;

            try
            {
                // Attempt to get the Type objects for VSE classes by their fully qualified names.
                Type passionManagerType = AccessTools.TypeByName("VSE.Passions.PassionManager");
                Type passionDefType = AccessTools.TypeByName("VSE.Passions.PassionDef");

                // Check if the types were found.
                if (passionManagerType == null || passionDefType == null) {
                    Log.Warning("[AutoWork Compat] Could not find VSE PassionManager or PassionDef type via AccessTools. VSE compatibility features disabled.");
                    VSEReflectionSuccess = false;
                    return; // Stop if types aren't found.
                }

                // Attempt to get the specific method and field using AccessTools.
                VSE_PassionToDefMethod = AccessTools.Method(passionManagerType, "PassionToDef", new Type[] { typeof(Passion) });
                VSE_LearnRateFactorField = AccessTools.Field(passionDefType, "learnRateFactor");

                // Check if both the method and field were successfully found.
                if (VSE_PassionToDefMethod != null && VSE_LearnRateFactorField != null)
                {
                    VSEReflectionSuccess = true;
                    // Log.Message("[AutoWork Compat] VSE reflection info successfully obtained (Method and Field)."); // Optional success log
                }
                else
                {
                    // Log specific warnings if either part failed.
                    if (VSE_PassionToDefMethod == null) Log.Warning("[AutoWork Compat] Could not find VSE method PassionManager.PassionToDef via AccessTools.");
                    if (VSE_LearnRateFactorField == null) Log.Warning("[AutoWork Compat] Could not find VSE field PassionDef.learnRateFactor via AccessTools.");
                    VSEReflectionSuccess = false;
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions during the reflection process.
                Log.Error($"[AutoWork Compat] Exception during VSE reflection initialization: {ex}");
                VSEReflectionSuccess = false; // Ensure failure state on exception.
            }
        }
    }
}