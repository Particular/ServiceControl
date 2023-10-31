﻿namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Linq;

    public class PersistenceManifest
    {
        public string Version { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string TypeName { get; set; }

        public bool IsSupported { get; set; } = true;

        public Setting[] Settings { get; set; } = Array.Empty<Setting>();

        public string[] SettingsWithPathsToCleanup { get; set; } = Array.Empty<string>();

        public string[] Aliases { get; set; } = Array.Empty<string>();

        internal bool IsMatch(string persistenceType) =>
            string.Equals(TypeName, persistenceType, StringComparison.Ordinal) // Type names are case-sensitive
            || string.Equals(Name, persistenceType, StringComparison.OrdinalIgnoreCase)
            || Aliases.Contains(persistenceType);

        public class Setting
        {
            public string Name { get; set; }
            public string DefaultValue { get; set; }
            public bool Mandatory { get; set; }
        }
    }
}