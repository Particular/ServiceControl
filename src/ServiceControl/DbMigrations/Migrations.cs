﻿namespace Particular.ServiceControl.DbMigrations
{
    using System;
    using System.Collections.Generic;

    public class Migrations
    {
        public const string DocumentId = "Settings/Migrations";

        public List<Migration> AppliedMigrations { get; set; } = new List<Migration>();

        public void Add(string migrationId)
        {
            AppliedMigrations.Add(new Migration
            {
                MigrationId = migrationId,
                DateApplied = DateTime.UtcNow
            });
        }

        public class Migration
        {
            public string MigrationId { get; set; }
            public DateTime DateApplied { get; set; }
        }
    }

}