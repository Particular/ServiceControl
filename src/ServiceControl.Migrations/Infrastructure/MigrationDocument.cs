namespace ServiceControl.Migrations
{
    using System;

    public class MigrationDocument
    {
        public MigrationDocument()
        {
            RunOn = DateTimeOffset.UtcNow;
        }

        public string Id { get; set; }
        public DateTimeOffset RunOn { get; set; }
    }
}