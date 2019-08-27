namespace NServiceBus
{
    using System.IO;
    using Settings;
    using Transport;

    public class ServiceControlLearningTransport : LearningTransport
    {
        public override bool RequiresConnectionString => true;

        public override string ExampleConnectionStringForErrorMessage => @"c:\path\containing\solution";

        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            settings.Set(StorageLocationKey, Path.Combine(connectionString, ".learningtransport"));

            return base.Initialize(settings, connectionString);
        }

        public const string StorageLocationKey = "LearningTransport.StoragePath";
    }
}
