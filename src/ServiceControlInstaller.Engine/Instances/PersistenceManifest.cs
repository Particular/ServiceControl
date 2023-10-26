namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;
    using System.Linq;

    public class PersistenceManifest
    {
        public string Version { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string TypeName { get; set; }

        public IList<Setting> Settings { get; set; } = new List<Setting>();

        public IList<string> SettingsWithPathsToCleanup { get; set; } = new List<string>(); public string[] Aliases { get; set; }

        internal bool IsMatch(string persistenceType) =>
            string.Compare(TypeName, persistenceType, false) == 0 // Type names are case-sensitive
            || string.Compare(Name, persistenceType, true) == 0
            || AliasesContain(persistenceType);

        bool AliasesContain(string transportType)
        {
            if (Aliases == null)
            {
                return false;
            }

            return Aliases.Contains(transportType);
        }

        public class Setting
        {
            public string Name { get; set; }
            public string DefaultValue { get; set; }
            public bool Mandatory { get; set; }
        }
    }
}