namespace Particular.ServiceControl.Upgrade
{
    public class StaleIndexInfoStore
    {
        public void Store(StaleIndexInfo info)
        {
            current = info;
        }

        public StaleIndexInfo Get()
        {
            return current;
        }

        public static readonly StaleIndexInfo NotInProgress = new StaleIndexInfo { InProgress = false, StartedAt = null };
        StaleIndexInfo current = NotInProgress;
    }
}