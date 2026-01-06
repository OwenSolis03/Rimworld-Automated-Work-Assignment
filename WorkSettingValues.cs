using Verse;

namespace Automated_Work_Assignment
{
    /// <summary>
    /// Defines the configuration settings for a single work type.
    /// UPDATED: Ahora incluye passionWeight y fallbackPriority.
    /// </summary>
    public class WorkSettingValues : IExposable
    {
        public int count = 3;
        public int priority = 3;
        public float percentage = 1f;
        public bool usePercentage = false;

        /// <summary>
        /// NUEVA FEATURE: Peso de la pasión en el cálculo de suitability
        /// 0.0 = Ignora pasión completamente (solo skill)
        /// 1.0 = Balance default (comportamiento original)
        /// 2.0 = Doble peso a la pasión
        /// 3.0 = Máximo énfasis en pasión
        /// </summary>
        public float passionWeight = 1f;

        /// <summary>
        /// NUEVA FEATURE: Prioridad para colonos NO seleccionados
        /// 0 = Deshabilitar (comportamiento actual)
        /// 1-4 = Asignar prioridad de respaldo
        /// Útil para Hauling/Cleaning donde quieres que todos ayuden si están libres
        /// </summary>
        public int fallbackPriority = 0;

        public WorkSettingValues() { }

        public WorkSettingValues(int count, int priority)
        {
            this.count = count;
            this.priority = priority;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref count, "count", 3);
            Scribe_Values.Look(ref priority, "priority", 3);
            Scribe_Values.Look(ref percentage, "percentage", 1f);
            Scribe_Values.Look(ref usePercentage, "usePercentage", false);
            Scribe_Values.Look(ref passionWeight, "passionWeight", 1f);
            Scribe_Values.Look(ref fallbackPriority, "fallbackPriority", 0);

            // Post-Load Validation
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (priority < 1) priority = 1;
                if (priority > 4) priority = 4;
                if (count < 0) count = 0;
                if (percentage < 0f) percentage = 0f;
                if (percentage > 1f) percentage = 1f;
                if (passionWeight < 0f) passionWeight = 0f;
                if (passionWeight > 3f) passionWeight = 3f;
                if (fallbackPriority < 0) fallbackPriority = 0;
                if (fallbackPriority > 4) fallbackPriority = 4;
            }
        }
    }
}