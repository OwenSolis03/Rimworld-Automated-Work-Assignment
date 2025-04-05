using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Contains Harmony patches applied by the mod.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        /// <summary>
        /// Static constructor. Applies Harmony patches on startup.
        /// </summary>
        static HarmonyPatches()
        {
            try
            {
                var harmony = new Harmony("Ekinox0310.AutomatedWorkAssignment");
                Log.Message("[AutoWork] Applying Harmony patches...");

                // --- Patch for adding buttons to the vanilla Work Tab ---
                var originalWorkTab = AccessTools.Method(typeof(MainTabWindow_Work), nameof(MainTabWindow_Work.DoWindowContents));
                var postfixWorkTab = new HarmonyMethod(typeof(HarmonyPatches), nameof(WorkTabPostfix));

                if (originalWorkTab != null && postfixWorkTab != null)
                {
                    harmony.Patch(originalWorkTab, postfix: postfixWorkTab);
                    Log.Message("[AutoWork] Patched MainTabWindow_Work.DoWindowContents successfully.");
                }
                else
                {
                    Log.Error("[AutoWork] Failed to find method or postfix for MainTabWindow_Work patch.");
                }
                // -------------------------------------------------------

                Log.Message("[AutoWork] Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception during Harmony patching: {ex}");
            }
        }

        /// <summary>
        /// Harmony Postfix for MainTabWindow_Work.DoWindowContents.
        /// Adds mod buttons (Toggle, Refresh, Settings) to the vanilla Work tab.
        /// Now uses per-save settings data.
        /// </summary>
        /// <param name="rect">The Rect representing the area of the Work tab window.</param>
        public static void WorkTabPostfix(Rect rect)
        {
            try
            {
                var saveData = AutomatedWorkAssignmentMod.CurrentData;

                if (saveData == null)
                {
                    // Log.ErrorOnce("[AutoWork] Save data is null in WorkTabPostfix!", 846522); // Optional log
                    return;
                }

                // --- Button Dimensions and Positioning ---
                const float buttonHeight = 30f;
                const float buttonWidth = 100f;
                const float padding = 5f;
                const float topMargin = 5f;

                // --- Toggle Mod Button ---
                Rect toggleButtonRect = new Rect(
                    rect.width - buttonWidth - padding, topMargin, buttonWidth, buttonHeight
                );
                string toggleLabel = saveData.modEnabled ? "AWA_ModOn".Translate() : "AWA_ModOff".Translate();
                Color originalColor = GUI.color;
                GUI.color = saveData.modEnabled ? Color.green : Color.red;

                if (Widgets.ButtonText(toggleButtonRect, toggleLabel))
                {
                    saveData.modEnabled = !saveData.modEnabled;
                    if (!saveData.modEnabled)
                    {
                        Log.Message("[AutoWork] Mod Disabled via UI button (Per-Save). Assignment logic paused.");
                    }
                    else
                    {
                        Log.Message("[AutoWork] Mod Enabled via UI button (Per-Save). Triggering refresh...");
                        WorkAssigner.RefreshAssignments();
                    }
                }
                GUI.color = originalColor;

                // --- Refresh Button ---
                Rect refreshButtonRect = new Rect(
                    toggleButtonRect.x - buttonWidth - padding, topMargin, buttonWidth, buttonHeight
                );
                if (Widgets.ButtonText(refreshButtonRect, "AWA_RefreshButton".Translate()))
                {
                    if (saveData.modEnabled)
                    {
                        Log.Message("[AutoWork] Manual refresh triggered via UI button.");
                        WorkAssigner.RefreshAssignments();
                    }
                    else
                    {
                        Messages.Message("AWA_ModDisabledMessage".Translate(), MessageTypeDefOf.CautionInput);
                    }
                }

                // --- Settings Button ---
                Rect settingsButtonRect = new Rect(
                    refreshButtonRect.x - buttonWidth - padding, topMargin, buttonWidth, buttonHeight
                );
                if (Widgets.ButtonText(settingsButtonRect, "AWA_SettingsButton".Translate()))
                {
                    Mod modInstance = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>();
                    if (modInstance != null)
                    {
                        Find.WindowStack.Add(new Dialog_ModSettings(modInstance));
                    }
                    else
                    {
                        Log.Error("[AutoWork] Could not find mod instance to open settings.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception in WorkTabPostfix: {ex}");
            }
        }
    }
}