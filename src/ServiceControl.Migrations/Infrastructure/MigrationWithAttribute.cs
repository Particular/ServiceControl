namespace ServiceControl.Migrations
{
    using System;

    internal class MigrationWithAttribute
    {
        public Func<Migration> Migration { get; set; }
        public MigrationAttribute Attribute { get; set; }
    }
}