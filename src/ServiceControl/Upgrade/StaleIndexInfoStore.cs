namespace Particular.ServiceControl.Upgrade
{
    public class StaleIndexInfoStore
    {
        public static readonly StaleIndexInfo NotInProgress = new StaleIndexInfo { InProgress = false, StartedAt = null };
        private StaleIndexInfo current = NotInProgress;
        
        public void Store(StaleIndexInfo info)
        {
            current = info;
        }

        public StaleIndexInfo Get()
        {
            return current;
        }
    }
}