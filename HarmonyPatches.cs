using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("Ekinox0310.AutomatedWorkAssignment");
            Log.Message("[AutoWork] Applying Harmony patches...");

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

            Log.Message("[AutoWork] Harmony patches applied.");
        }

        public static void WorkTabPostfix(Rect rect)
        {
            AutomatedWorkSettings settings = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>().GetSettings<AutomatedWorkSettings>();

            if (settings == null) {
                Log.ErrorOnce("[AutoWork] Settings instance is null in WorkTabPostfix!", 846521);
                return;
            }

            float buttonHeight = 30f;
            float buttonWidth = 100f;
            float padding = 5f;
            float topMargin = 5f;

            Rect toggleButtonRect = new Rect(
                rect.width - buttonWidth - padding,
                topMargin,
                buttonWidth,
                buttonHeight
            );
            string toggleLabel = settings.modEnabled ? "AWA_ModOn".Translate() : "AWA_ModOff".Translate();
            Color originalColor = GUI.color;
            GUI.color = settings.modEnabled ? Color.green : Color.red;
            if (Widgets.ButtonText(toggleButtonRect, toggleLabel))
            {
                settings.modEnabled = !settings.modEnabled;
                if (!settings.modEnabled) {
                    Log.Message("[AutoWork] Mod Disabled via UI button. Assignment logic paused.");
                } else {
                    Log.Message("[AutoWork] Mod Enabled via UI button. Triggering refresh...");
                    WorkAssigner.RefreshAssignments();
                }
            }
            GUI.color = originalColor;

            Rect refreshButtonRect = new Rect(
                toggleButtonRect.x - buttonWidth - padding,
                topMargin,
                buttonWidth,
                buttonHeight
            );
            if (Widgets.ButtonText(refreshButtonRect, "AWA_RefreshButton".Translate()))
            {
                if (settings.modEnabled)
                {
                    WorkAssigner.RefreshAssignments();
                } else {
                    Messages.Message("AWA_ModDisabledMessage".Translate(), MessageTypeDefOf.CautionInput);
                }
            }

            Rect settingsButtonRect = new Rect(
                refreshButtonRect.x - buttonWidth - padding,
                topMargin,
                buttonWidth,
                buttonHeight
            );
            if (Widgets.ButtonText(settingsButtonRect, "AWA_SettingsButton".Translate()))
            {
                Mod modInstance = LoadedModManager.GetMod<AutomatedWorkAssignmentMod>();
                if (modInstance != null)
                {
                    Find.WindowStack.Add(new Dialog_ModSettings(modInstance));
                } else {
                    Log.Error("[AutoWork] Could not find mod instance to open settings.");
                }
            }
        }
    }
}