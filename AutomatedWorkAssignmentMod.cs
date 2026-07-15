using System;
using UnityEngine;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// The main entry point for the Automated Work Assignment mod.
    /// Registers the mod settings category and automatically opens the
    /// custom draggable/resizable settings window when the user selects this mod.
    /// </summary>
    public class AutomatedWorkAssignmentMod : Mod
    {
        /// <summary>
        /// Gets the save-specific data component for the current game.
        /// Returns null if no game is currently loaded.
        /// </summary>
        internal static AutomatedWork_SaveData CurrentData => Current.Game?.GetComponent<AutomatedWork_SaveData>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomatedWorkAssignmentMod"/> class.
        /// </summary>
        /// <param name="content">The content pack associated with this mod.</param>
        public AutomatedWorkAssignmentMod(ModContentPack content) : base(content) { }

        /// <summary>
        /// Gets the name of the settings category to be displayed in the mod settings menu.
        /// </summary>
        /// <returns>The translated settings category name.</returns>
        public override string SettingsCategory() => "AWA_SettingsCategory".Translate();

        /// <summary>
        /// Called by the vanilla Dialog_ModSettings. Immediately closes it and opens
        /// our own <see cref="Dialog_AWASettings"/> instead, which supports drag and resize.
        /// </summary>
        /// <param name="inRect">The rectangular area provided by the vanilla dialog (unused).</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Only redirect once — if our window is already open, do nothing
            if (Find.WindowStack.IsOpen<Dialog_AWASettings>())
            {
                return;
            }

            // Close the vanilla mod settings dialog and open ours
            Find.WindowStack.TryRemove(typeof(RimWorld.Dialog_ModSettings), doCloseSound: false);
            Find.WindowStack.Add(new Dialog_AWASettings());
        }
    }
}