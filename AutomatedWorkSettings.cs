using System.Collections.Generic;
using Verse;

namespace Automated_Work_Assignment 
{


    public class AutomatedWorkSettings : ModSettings
    {

        public bool modEnabled = true;
        public bool enableDailyRefresh = true;
        public Dictionary<string, WorkSettingValues> workSettings = new Dictionary<string, WorkSettingValues>();
        public List<string> excludedPawnIDs = new List<string>();


        private List<string> workSettingsKeysWorkingList;
        private List<WorkSettingValues> workSettingsValuesWorkingList;



        public WorkSettingValues GetWorkSetting(string workTypeDefName)
        {
            if (workSettings == null) { workSettings = new Dictionary<string, WorkSettingValues>(); }
            if (!workSettings.TryGetValue(workTypeDefName, out WorkSettingValues setting))
            {
                setting = new WorkSettingValues();
                workSettings.Add(workTypeDefName, setting);
            }
            if (setting.priority < 1) setting.priority = 1;
            if (setting.priority > 4) setting.priority = 4;
            if (setting.count < 0) setting.count = 0;
            return setting;
        }


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref enableDailyRefresh, "enableDailyRefresh", true); // <-- NUEVO: Guardar/Cargar opción diaria
            Scribe_Collections.Look(ref excludedPawnIDs, "excludedPawnIDs", LookMode.Value);
            Scribe_Collections.Look(ref workSettings, "workSettings", LookMode.Value, LookMode.Deep,
                ref workSettingsKeysWorkingList, ref workSettingsValuesWorkingList);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (workSettings == null) { workSettings = new Dictionary<string, WorkSettingValues>(); }
                if (excludedPawnIDs == null) { excludedPawnIDs = new List<string>(); }
            }
        }
    }
}