// ----- ModDetector.cs (Con Inicialización Retrasada de Reflexión) -----

using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Automated_Work_Assignment
{
    [StaticConstructorOnStartup]
    public static class ModDetector
    {
        public static bool VSEIsActive { get; private set; }
        public static bool AlphaSkillsIsActive { get; private set; }

        // --- Info VSE - Solo guardar Info, no tipos/delegados de VSE ---
        internal static MethodInfo VSE_PassionToDefMethod { get; private set; } = null;     // Cambiado a internal static
        internal static PropertyInfo VSE_LearnRateFactorProperty { get; private set; } = null; // Cambiado a internal static
        private static bool reflectionAttempted = false;
        internal static bool VSEReflectionSuccess { get; private set; } = false; // Cambiado a internal static

        static ModDetector()
        {
            // Detección segura al inicio
            VSEIsActive = ModLister.GetActiveModWithIdentifier("vanillaexpanded.skills", true) != null; // Usa tu ID verificado
            AlphaSkillsIsActive = ModLister.GetActiveModWithIdentifier("sarg.alphaskills", true) != null;   // Usa tu ID verificado

            Log.Message($"[AutoWork] Compatibility: Vanilla Skills Expanded {(VSEIsActive ? "DETECTED" : "NOT detected")}.");
            Log.Message($"[AutoWork] Compatibility: Alpha Skills {(AlphaSkillsIsActive ? "DETECTED" : "NOT detected")}.");

            // La inicialización de reflexión se hará bajo demanda
        }

        // Método para asegurar que se intente la inicialización una vez cuando se necesite
        internal static void EnsureReflectionInitialized() // Cambiado a internal static
        {
            if (reflectionAttempted) return;
            reflectionAttempted = true;

            if (!VSEIsActive) return; // No intentar si VSE no está activo

            // Log.Message("[AutoWork Compat] Attempting VSE reflection initialization..."); // Log opcional
            try
            {
                Type passionManagerType = AccessTools.TypeByName("VSE.Passions.PassionManager");
                Type passionDefType = AccessTools.TypeByName("VSE.Passions.PassionDef");

                if (passionManagerType == null || passionDefType == null) {
                    Log.Warning("[AutoWork Compat] Could not find VSE PassionManager or PassionDef type via AccessTools."); return;
                }

                VSE_PassionToDefMethod = AccessTools.Method(passionManagerType, "PassionToDef", new Type[] { typeof(Passion) });
                VSE_LearnRateFactorProperty = AccessTools.Property(passionDefType, "learnRateFactor");

                if (VSE_PassionToDefMethod != null && VSE_LearnRateFactorProperty != null && VSE_LearnRateFactorProperty.GetGetMethod() != null) {
                    VSEReflectionSuccess = true;
                    // Log.Message("[AutoWork Compat] VSE reflection info successfully obtained."); // Log opcional
                } else { /* Logs de Warning específicos */ }
            }
            catch (Exception ex) {
                Log.Error($"[AutoWork Compat] Exception during VSE reflection initialization: {ex}");
                VSEReflectionSuccess = false;
            }
        }
    }
}