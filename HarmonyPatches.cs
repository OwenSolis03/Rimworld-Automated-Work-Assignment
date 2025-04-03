using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Contains Harmony patches applied by the mod.
    /// Uses StaticConstructorOnStartup to apply patches when the game loads the mod.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        /// <summary>
        /// Static constructor automatically called on game startup. Applies Harmony patches.
        /// </summary>
        static HarmonyPatches()
        {
            try // Add try-catch around the whole patching process for robustness
            {
                // Create a Harmony instance with a unique ID for the mod
                var harmony = new Harmony("Ekinox0310.AutomatedWorkAssignment");
                Log.Message("[AutoWork] Applying Harmony patches...");

                // --- Patch for adding buttons to the vanilla Work Tab ---
                // Get the original method (DoWindowContents in the vanilla work tab)
                var originalWorkTab = AccessTools.Method(typeof(MainTabWindow_Work), nameof(MainTabWindow_Work.DoWindowContents));
                // Get the postfix method defined in this class
                var postfixWorkTab = new HarmonyMethod(typeof(HarmonyPatches), nameof(WorkTabPostfix));

                // Check if both methods were found successfully
                if (originalWorkTab != null && postfixWorkTab != null)
                {
                    // Apply the postfix patch
                    harmony.Patch(originalWorkTab, postfix: postfixWorkTab);
                    Log.Message("[AutoWork] Patched MainTabWindow_Work.DoWindowContents successfully.");
                }
                else
                {
                    // Log an error if methods couldn't be found (e.g., game update changed them)
                    Log.Error("[AutoWork] Failed to find method or postfix for MainTabWindow_Work patch.");
                }
                // -------------------------------------------------------

                // Add other patches here if needed in the future

                Log.Message("[AutoWork] Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"[AutoWork] Exception during Harmony patching: {ex}");
            }
        }

        /// <summary>
        /// Harmony Postfix method for MainTabWindow_Work.DoWindowContents.
        /// Adds mod-specific buttons (Toggle, Refresh, Settings) to the top-right
        /// of the vanilla Work tab window.
        /// Includes exception handling to prevent UI errors from breaking the whole tab.
        /// </summary>
        /// <param name="rect">The Rect representing the area of the Work tab window.</param>
        public static void WorkTabPostfix(Rect rect)
        {
            try // Add try-catch around the postfix logic to prevent breaking the UI
            {
                // Safely get mod settings
                AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>()?.GetSettings<AutomatedWorkSettings>();

                // Check if settings are loaded correctly
                if (settings == null)
                {
                    // Log error once to avoid spamming the log
                    Log.ErrorOnce("[AutoWork] Settings instance is null in WorkTabPostfix!", 846521);
                    return; // Exit if settings are not available
                }

                // --- Button Dimensions and Positioning ---
                const float buttonHeight = 30f;
                const float buttonWidth = 100f;
                const float padding = 5f;
                const float topMargin = 5f; // Margin from the top edge of the window

                // --- Toggle Mod Button ---
                // Positioned at the top-right
                Rect toggleButtonRect = new Rect(
                    rect.width - buttonWidth - padding, // X position (right edge - width - padding)
                    topMargin,                          // Y position
                    buttonWidth,                        // Width
                    buttonHeight                        // Height
                );
                // Set label and color based on mod enabled status
                string toggleLabel = settings.modEnabled ? "AWA_ModOn".Translate() : "AWA_ModOff".Translate();
                Color originalColor = GUI.color; // Store original GUI color
                GUI.color = settings.modEnabled ? Color.green : Color.red; // Set color based on status

                // Draw the button and check if clicked
                if (Widgets.ButtonText(toggleButtonRect, toggleLabel))
                {
                    // Toggle the mod's enabled status
                    settings.modEnabled = !settings.modEnabled;
                    // Log the change and potentially trigger a refresh if enabled
                    if (!settings.modEnabled)
                    {
                        Log.Message("[AutoWork] Mod Disabled via UI button. Assignment logic paused.");
                    }
                    else
                    {
                        Log.Message("[AutoWork] Mod Enabled via UI button. Triggering refresh...");
                        // Trigger refresh immediately when enabled via button
                        WorkAssigner.RefreshAssignments();
                    }
                    // Settings are saved automatically by RimWorld later
                }
                GUI.color = originalColor; // Restore original GUI color

                // --- Refresh Button ---
                // Positioned to the left of the toggle button
                Rect refreshButtonRect = new Rect(
                    toggleButtonRect.x - buttonWidth - padding, // X position
                    topMargin,                                  // Y position
                    buttonWidth,                                // Width
                    buttonHeight                                // Height
                );
                // Draw the button and check if clicked
                if (Widgets.ButtonText(refreshButtonRect, "AWA_RefreshButton".Translate()))
                {
                    // Only refresh if the mod is currently enabled
                    if (settings.modEnabled)
                    {
                        Log.Message("[AutoWork] Manual refresh triggered via UI button.");
                        WorkAssigner.RefreshAssignments();
                    }
                    else
                    {
                        // Show a message to the user if they try to refresh while disabled
                        Messages.Message("AWA_ModDisabledMessage".Translate(), MessageTypeDefOf.CautionInput);
                    }
                }

                // --- Settings Button ---
                // Positioned to the left of the refresh button
                Rect settingsButtonRect = new Rect(
                    refreshButtonRect.x - buttonWidth - padding, // X position
                    topMargin,                                   // Y position
                    buttonWidth,                                 // Width
                    buttonHeight                                 // Height
                );
                // Draw the button and check if clicked
                if (Widgets.ButtonText(settingsButtonRect, "AWA_SettingsButton".Translate()))
                {
                    // Get the instance of this mod to open its settings window
                    Mod modInstance = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>();
                    if (modInstance != null)
                    {
                        // Open the standard mod settings dialog for this mod
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
                // This catch prevents errors in the postfix from breaking the entire Work tab rendering
            }
        }
    }
}