namespace ServiceControl.HeartbeatMonitoring
{
    public class HeartbeatsStats
    {
        public HeartbeatsStats(int active, int dead)
        {
            Active = active;
            Dead = dead;
        }

        public readonly int Active;
        public readonly int Dead;
    }
}