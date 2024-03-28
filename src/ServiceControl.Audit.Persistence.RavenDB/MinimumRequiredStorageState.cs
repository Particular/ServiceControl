namespace ServiceControl.Audit.Persistence.RavenDB
{
    public class MinimumRequiredStorageState
    {
        public bool CanIngestMore { get; set; } = true;
    }
}