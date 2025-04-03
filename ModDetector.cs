using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

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
        /// <summary>
        /// Gets a value indicating whether Vanilla Skills Expanded is currently active.
        /// Determined once at startup.
        /// </summary>
        public static bool VSEIsActive { get; private set; }

        /// <summary>
        /// Gets a value indicating whether Alpha Skills is currently active.
        /// Determined once at startup. (Note: Currently only detected, not used elsewhere in provided code).
        /// </summary>
        public static bool AlphaSkillsIsActive { get; private set; }

        // --- VSE Reflection Info ---
        // Stored as internal static to be accessible within the assembly (e.g., by WorkAssigner).
        // Initialized lazily by EnsureReflectionInitialized().

        /// <summary>
        /// Cached MethodInfo for VSE's PassionManager.PassionToDef(Passion).
        /// Null if VSE is not active or reflection failed.
        /// </summary>
        internal static MethodInfo VSE_PassionToDefMethod { get; private set; } = null;

        /// <summary>
        /// Cached PropertyInfo for VSE's PassionDef.learnRateFactor.
        /// Null if VSE is not active or reflection failed.
        /// </summary>
        internal static PropertyInfo VSE_LearnRateFactorProperty { get; private set; } = null;

        /// <summary>
        /// Flag indicating whether the reflection attempt for VSE has already been made.
        /// Prevents redundant reflection attempts.
        /// </summary>
        private static bool reflectionAttempted = false;

        /// <summary>
        /// Gets a value indicating whether the reflection attempt for VSE members was successful.
        /// True only if VSE is active AND the necessary methods/properties were found via reflection.
        /// </summary>
        internal static bool VSEReflectionSuccess { get; private set; } = false;

        /// <summary>
        /// Static constructor called once when the game loads the mod assembly.
        /// Detects active mods using ModLister.
        /// </summary>
        static ModDetector()
        {
            // Safely detect active mods using their unique identifiers
            // Use verified IDs for accuracy. Second parameter 'true' checks only enabled mods.
            VSEIsActive = ModLister.GetActiveModWithIdentifier("vanillaexpanded.skills", true) != null;
            AlphaSkillsIsActive = ModLister.GetActiveModWithIdentifier("sarg.alphaskills", true) != null; // Example ID, verify if needed

            // Log detection results for debugging/information
            Log.Message($"[AutoWork] Compatibility: Vanilla Skills Expanded {(VSEIsActive ? "DETECTED" : "NOT detected")}.");
            Log.Message($"[AutoWork] Compatibility: Alpha Skills {(AlphaSkillsIsActive ? "DETECTED" : "NOT detected")}.");

            // Note: VSE reflection initialization is deferred until actually needed (see EnsureReflectionInitialized)
        }

        /// <summary>
        /// Ensures that the reflection process to find VSE members has been attempted exactly once.
        /// This method performs the actual reflection if VSE is active and it hasn't been tried yet.
        /// It's designed to be called just before the reflection info is needed (lazy initialization).
        /// </summary>
        internal static void EnsureReflectionInitialized()
        {
            // Only attempt reflection once
            if (reflectionAttempted) return;
            reflectionAttempted = true;

            // Don't attempt reflection if VSE isn't active
            if (!VSEIsActive) return;

            // Log optional message indicating reflection attempt
            // Log.Message("[AutoWork Compat] Attempting VSE reflection initialization...");

            try // Wrap reflection calls in try-catch as they can fail
            {
                // Get Types using AccessTools (safer than Type.GetType for modded types)
                Type passionManagerType = AccessTools.TypeByName("VSE.Passions.PassionManager");
                Type passionDefType = AccessTools.TypeByName("VSE.Passions.PassionDef");

                // Check if types were found
                if (passionManagerType == null || passionDefType == null) {
                    Log.Warning("[AutoWork Compat] Could not find VSE PassionManager or PassionDef type via AccessTools. VSE compatibility features disabled.");
                    VSEReflectionSuccess = false; // Ensure flag is false
                    return; // Exit if types aren't found
                }

                // Get specific Method and Property info using AccessTools
                // Note: Requires exact method/property names and parameter types (if applicable)
                VSE_PassionToDefMethod = AccessTools.Method(passionManagerType, "PassionToDef", new Type[] { typeof(Passion) });
                VSE_LearnRateFactorProperty = AccessTools.Property(passionDefType, "learnRateFactor");

                // Check if reflection was successful (members found and property has a getter)
                if (VSE_PassionToDefMethod != null && VSE_LearnRateFactorProperty != null && VSE_LearnRateFactorProperty.GetGetMethod() != null)
                {
                    VSEReflectionSuccess = true; // Mark reflection as successful
                    // Log.Message("[AutoWork Compat] VSE reflection info successfully obtained."); // Optional success log
                }
                else
                {
                    // Log specific warnings if reflection failed partially or completely
                    if (VSE_PassionToDefMethod == null) Log.Warning("[AutoWork Compat] Could not find VSE method PassionManager.PassionToDef via AccessTools.");
                    if (VSE_LearnRateFactorProperty == null) Log.Warning("[AutoWork Compat] Could not find VSE property PassionDef.learnRateFactor via AccessTools.");
                    else if (VSE_LearnRateFactorProperty.GetGetMethod() == null) Log.Warning("[AutoWork Compat] VSE property PassionDef.learnRateFactor does not have a public getter.");

                    VSEReflectionSuccess = false; // Ensure flag is false on failure
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exceptions during the reflection process
                Log.Error($"[AutoWork Compat] Exception during VSE reflection initialization: {ex}");
                VSEReflectionSuccess = false; // Ensure flag is false on exception
            }
        }
    }
}