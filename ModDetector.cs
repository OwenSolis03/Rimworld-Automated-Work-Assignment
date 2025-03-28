using Verse;

namespace Automated_Work_Assignment
{
    [StaticConstructorOnStartup]
    public static class ModDetector
    {

        public static bool VSEIsActive { get; private set; }
        public static bool AlphaSkillsIsActive { get; private set; }

        static ModDetector()
        {

            VSEIsActive = ModLister.GetActiveModWithIdentifier("vanillaexpanded.skills") != null;
            AlphaSkillsIsActive = ModLister.GetActiveModWithIdentifier("sarg.alphaskills") != null; 


            if (VSEIsActive)
            {
                Log.Message("[AutoWork] Compatibility: Vanilla Skills Expanded DETECTED.");
            }
            else
            {
                Log.Message("[AutoWork] Compatibility: Vanilla Skills Expanded NOT detected.");
            }

            if (AlphaSkillsIsActive)
            {
                Log.Message("[AutoWork] Compatibility: Alpha Skills DETECTED.");

            }
            else
            {
                Log.Message("[AutoWork] Compatibility: Alpha Skills NOT detected.");
            }
        }
    }
}