namespace ServiceControl.Recoverability
{

    public class ReclassifyErrorSettings
    {
        public const string IdentifierCase = "ReclassifyErrorSettings/1";

        public ReclassifyErrorSettings()
        {
            Id = IdentifierCase;
        }

        public string Id { get; set; }

        public bool ReclassificationDone { get; set; }
    }
}