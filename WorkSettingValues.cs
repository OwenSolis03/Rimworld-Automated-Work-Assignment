using Verse;

namespace Automated_Work_Assignment
{
    public class WorkSettingValues : IExposable
    {
        public int count = 0;
        public int priority = 3;


        public WorkSettingValues() { }


        public WorkSettingValues(int count, int priority)
        {
            this.count = count;
            this.priority = priority;
        }
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref count, "count", 0);
            Scribe_Values.Look(ref priority, "priority", 3);
        }
    }
}