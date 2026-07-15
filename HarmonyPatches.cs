using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Contains and applies Harmony patches required for the Automated Work Assignment mod.
    /// This static class utilizes the Harmony library to modify vanilla RimWorld methods
    /// at runtime, enabling the mod's features to integrate with the game's UI and logic.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        /// <summary>
        /// Initializes the Harmony patches when the game starts.
        /// This static constructor is called automatically by RimWorld upon loading,
        /// ensuring that the necessary patches are applied before they are needed.
        /// It identifies the target methods and applies the corresponding postfix patches.
        /// </summary>
        static HarmonyPatches()
        {
            try
            {
                // Create a unique Harmony instance for this mod.
                var harmony = new Harmony("Ekinox0310.AutomatedWorkAssignment");
                Log.Message("[AutoWork] Applying Harmony patches...");

                // --- Patch for adding buttons to the vanilla Work Tab ---
                // Target the method responsible for drawing the Work tab's contents.
                var originalWorkTab = AccessTools.Method(typeof(MainTabWindow_Work), nameof(MainTabWindow_Work.DoWindowContents));
                // Define the method in this class that should run *after* the original method.
                var postfixWorkTab = new HarmonyMethod(typeof(HarmonyPatches), nameof(WorkTabPostfix));

                // Apply the patch if both the original method and the postfix method were found.
                if (originalWorkTab != null && postfixWorkTab != null)
                {
                    harmony.Patch(originalWorkTab, postfix: postfixWorkTab);
                    Log.Message("[AutoWork] Patched MainTabWindow_Work.DoWindowContents successfully.");
                }
                else
                {
                    Log.Error("[AutoWork] Failed to find method or postfix for MainTabWindow_Work patch. UI buttons will not be added.");
                }
                // -------------------------------------------------------

                // --- Patch for Experimental Heuristics ---
                var originalSetPriority = AccessTools.Method(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority));
                var postfixSetPriority = new HarmonyMethod(typeof(HarmonyPatches), nameof(SetPriorityPostfix));
                if (originalSetPriority != null && postfixSetPriority != null)
                {
                    harmony.Patch(originalSetPriority, postfix: postfixSetPriority);
                    Log.Message("[AutoWork] Patched Pawn_WorkSettings.SetPriority for heuristics.");
                }
                // -------------------------------------------------------

                Log.Message("[AutoWork] Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception during Harmony patching process: {ex}");
            }
        }

        /// <summary>
        /// Harmony Postfix patch for the <see cref="MainTabWindow_Work.DoWindowContents"/> method.
        /// This method is executed *after* the original DoWindowContents method runs, allowing
        /// it to draw additional UI elements on top of the standard Work tab.
        /// It adds the mod's control buttons (Toggle Mod, Refresh, Settings) to the top-right
        /// corner of the Work tab, interacting with the current game's <see cref="AutomatedWork_SaveData"/>.
        /// </summary>
        /// <param name="rect">The <see cref="Rect"/> provided by the game, defining the available drawing area for the Work tab window content.</param>
        public static void WorkTabPostfix(Rect rect)
        {
            try
            {
                // Retrieve the mod's data specific to the current save game.
                var saveData = AutomatedWorkAssignmentMod.CurrentData;

                // If no save data is loaded (e.g., on the main menu), do nothing.
                if (saveData == null)
                {
                    return;
                }

                // --- Button Dimensions and Positioning ---
                const float buttonHeight = 30f; // Standard height for the buttons.
                const float buttonWidth = 100f; // Standard width for the buttons.
                const float padding = 5f;       // Spacing between buttons.
                const float topMargin = 5f;     // Distance from the top edge of the window content area.

                // --- Toggle Mod Button ---
                // Calculate position based on the window width and button dimensions.
                Rect toggleButtonRect = new Rect(
                    rect.width - buttonWidth - padding, // X: Position from the right edge
                    topMargin,                          // Y: Position from the top edge
                    buttonWidth,                        // Width
                    buttonHeight                        // Height
                );
                // Dynamically set the button label based on the mod's enabled state in the save data.
                string toggleLabel = saveData.modEnabled ? "AWA_ModOn".Translate() : "AWA_ModOff".Translate();
                Color originalColor = GUI.color; // Store the original GUI color.
                // Change button color based on state (Green for ON, Red for OFF).
                GUI.color = saveData.modEnabled ? Color.green : Color.red;

                // Draw the button and check if it was clicked.
                if (Widgets.ButtonText(toggleButtonRect, toggleLabel))
                {
                    // Toggle the mod's enabled state in the save data.
                    saveData.modEnabled = !saveData.modEnabled;
                    if (!saveData.modEnabled)
                    {
                        Log.Message("[AutoWork] Mod Disabled via UI button (Per-Save). Assignment logic paused.");
                    }
                    else
                    {
                        Log.Message("[AutoWork] Mod Enabled via UI button (Per-Save). Triggering refresh...");
                        // Immediately refresh assignments when re-enabled.
                        WorkAssigner.RefreshAssignments();
                    }
                }
                GUI.color = originalColor; // Restore the original GUI color.

                // --- Refresh Button ---
                // Position it to the left of the toggle button.
                Rect refreshButtonRect = new Rect(
                    toggleButtonRect.x - buttonWidth - padding, // X: Offset from the toggle button
                    topMargin,                                  // Y: Same top margin
                    buttonWidth,                                // Width
                    buttonHeight                                // Height
                );
                // Draw the refresh button.
                if (Widgets.ButtonText(refreshButtonRect, "AWA_RefreshButton".Translate()))
                {
                    Log.Message("[AutoWork] Manual refresh triggered via UI button.");
                    WorkAssigner.RefreshAssignments(); // Trigger the assignment logic.
                }
                
                // --- Settings Button ---
                // Position it to the left of the refresh button.
                Rect settingsButtonRect = new Rect(
                    refreshButtonRect.x - buttonWidth - padding, // X: Offset from the refresh button
                    topMargin,                                   // Y: Same top margin
                    buttonWidth,                                 // Width
                    buttonHeight                                 // Height
                );
                // Draw the settings button.
                if (Widgets.ButtonText(settingsButtonRect, "AWA_SettingsButton".Translate()))
                {
                    // Find the instance of this mod to access its settings dialog.
                    Mod modInstance = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>();
                    if (modInstance != null)
                    {
                        // Open the standard mod settings dialog provided by RimWorld.
                        Find.WindowStack.Add(new Dialog_ModSettings(modInstance));
                    }
                    else
                    {
                        Log.Error("[AutoWork] Could not find AutomatedWorkAssignmentMod instance to open settings.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected errors during the UI drawing process.
                Log.Error($"[AutoWork] Exception in WorkTabPostfix UI drawing: {ex}");
            }
        }

        /// <summary>
        /// Harmony Postfix patch for Pawn_WorkSettings.SetPriority.
        /// Intercepts manual priority changes made by the player to feed into the heuristic learning module.
        /// </summary>
        public static void SetPriorityPostfix(Pawn_WorkSettings __instance, WorkTypeDef w, int priority)
        {
            if (WorkAssigner.IsRunningAutomatedRefresh) return; // Ignore automated changes

            var saveData = AutomatedWorkAssignmentMod.CurrentData;
            if (saveData == null || !saveData.enableExperimentalHeuristics) return;

            try
            {
                // Access the private 'pawn' field in Pawn_WorkSettings
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn != null && pawn.Map != null)
                {
                    var hm = Current.Game.GetComponent<Experimental.Heuristics.HeuristicManager>();
                    if (hm != null)
                    {
                        hm.OnPlayerChangedPriority(pawn.Map, pawn, w, priority);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception in SetPriorityPostfix heuristics: {ex}");
            }
        }
    }
}