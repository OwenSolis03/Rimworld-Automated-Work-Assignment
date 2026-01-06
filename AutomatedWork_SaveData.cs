using System.Collections.Generic;
using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// A RimWorld <see cref="GameComponent"/> dedicated to storing and managing all mod-related settings
    /// that are specific to a particular save game.
    /// UPDATED: Now includes AssignmentMode, per-job exclusions, passion priority, and emergency toggles.
    /// </summary>
    public class AutomatedWork_SaveData : GameComponent
    {
        // --- Core Settings ---
        
        public bool modEnabled = true;
        public bool enableDailyRefresh = true;

        /// <summary>
        /// NUEVA FEATURE: Modo de asignación activo
        /// Simple: Solo usa sliders de Simple Mode
        /// Expert: Solo usa reglas de Expert Mode (requiere reglas definidas)
        /// Hybrid: Expert tiene prioridad, Simple es fallback
        /// </summary>
        public enum AssignmentMode
        {
            Simple,
            Expert,
            Hybrid
        }
        public AssignmentMode assignmentMode = AssignmentMode.Simple;

        /// <summary>
        /// NUEVA FEATURE: Si true, Doctor y Firefighter se fuerzan a prioridad 1
        /// Si false, respetan las configuraciones del usuario
        /// </summary>
        public bool forceEmergencyPriorities = true;

        /// <summary>
        /// NUEVA FEATURE: En Expert Mode, ordena por pasión PRIMERO en lugar de por skill
        /// Permite entrenar colonos con pasión alta pero skill bajo
        /// </summary>
        public bool prioritizePassionInExpertMode = false;

        // --- Work Type Settings ---

        public Dictionary<string, WorkSettingValues> workSettings = new Dictionary<string, WorkSettingValues>();

        // --- Exclusion Lists ---

        /// <summary>
        /// Lista global de IDs de colonos excluidos completamente del sistema
        /// </summary>
        public List<string> excludedPawnIDs = new List<string>();

        /// <summary>
        /// Lista de trabajos (WorkTypeDef.defName) excluidos del sistema
        /// </summary>
        public List<string> excludedWorkTypeDefNames = new List<string>();

        /// <summary>
        /// NUEVA FEATURE: Exclusiones por trabajo específico
        /// Diccionario: WorkTypeDef.defName → List de Pawn.ThingID
        /// Ejemplo: {"Doctor": ["Colonist123"]} = Colonist123 nunca será doctor
        /// </summary>
        public Dictionary<string, List<string>> perJobExcludedPawnIDs = new Dictionary<string, List<string>>();

        // --- Private working lists for Scribe ---
        
        private List<string> workSettingsKeysWorkingList;
        private List<WorkSettingValues> workSettingsValuesWorkingList;
        private List<string> perJobExclusionKeysWorkingList;
        private List<List<string>> perJobExclusionValuesWorkingList;

        public AutomatedWork_SaveData(Game game) { }

        /// <summary>
        /// Obtiene o crea la configuración para un trabajo específico
        /// </summary>
        public WorkSettingValues GetWorkSetting(string workTypeDefName)
        {
            if (workSettings == null)
            {
                Log.Warning("[AutoWork] SaveData: workSettings was null in GetWorkSetting. Initializing.");
                workSettings = new Dictionary<string, WorkSettingValues>();
            }

            if (!workSettings.TryGetValue(workTypeDefName, out WorkSettingValues setting))
            {
                setting = new WorkSettingValues();
                workSettings.Add(workTypeDefName, setting);
            }

            // Validación de rangos
            if (setting.priority < 1) setting.priority = 1;
            if (setting.priority > 4) setting.priority = 4;
            if (setting.count < 0) setting.count = 0;
            if (setting.percentage < 0f) setting.percentage = 0f;
            if (setting.percentage > 1f) setting.percentage = 1f;

            return setting;
        }

        /// <summary>
        /// NUEVA FEATURE: Verifica si un colono está excluido de un trabajo específico
        /// </summary>
        public bool IsPawnExcludedFromJob(string pawnID, string workTypeDefName)
        {
            if (perJobExcludedPawnIDs == null) return false;
            if (!perJobExcludedPawnIDs.TryGetValue(workTypeDefName, out var excludedList)) return false;
            return excludedList?.Contains(pawnID) ?? false;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Settings básicos
            Scribe_Values.Look(ref modEnabled, "modEnabled_perSave", true);
            Scribe_Values.Look(ref enableDailyRefresh, "enableDailyRefresh_perSave", true);
            
            // NUEVAS FEATURES
            Scribe_Values.Look(ref assignmentMode, "assignmentMode", AssignmentMode.Simple);
            Scribe_Values.Look(ref forceEmergencyPriorities, "forceEmergencyPriorities", true);
            Scribe_Values.Look(ref prioritizePassionInExpertMode, "prioritizePassionInExpertMode", false);

            // Listas de exclusión
            Scribe_Collections.Look(ref excludedPawnIDs, "excludedPawnIDs_perSave", LookMode.Value);
            Scribe_Collections.Look(ref excludedWorkTypeDefNames, "excludedWorkTypeDefNames_perSave", LookMode.Value);

            // Configuraciones de trabajos
            Scribe_Collections.Look(ref workSettings, "workSettings_perSave", LookMode.Value, LookMode.Deep,
                ref workSettingsKeysWorkingList, ref workSettingsValuesWorkingList);

            // NUEVA FEATURE: Exclusiones por trabajo
            Scribe_Collections.Look(ref perJobExcludedPawnIDs, "perJobExcludedPawnIDs", 
                LookMode.Value, LookMode.Deep,
                ref perJobExclusionKeysWorkingList, ref perJobExclusionValuesWorkingList);

            // Post-load initialization
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workSettings == null) workSettings = new Dictionary<string, WorkSettingValues>();
                if (excludedPawnIDs == null) excludedPawnIDs = new List<string>();
                if (excludedWorkTypeDefNames == null) excludedWorkTypeDefNames = new List<string>();
                if (perJobExcludedPawnIDs == null) perJobExcludedPawnIDs = new Dictionary<string, List<string>>();
            }
        }
    }
}