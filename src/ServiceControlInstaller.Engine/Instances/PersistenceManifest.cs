namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;

    public class PersistenceManifest
    {
        public string Version { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string TypeName { get; set; }

        public IList<Setting> Settings { get; set; } = new List<Setting>();

        public IList<string> SettingsWithPathsToCleanup { get; set; } = new List<string>();

        public class Setting
        {
            public string Name { get; set; }
            public string DefaultValue { get; set; }
            public bool Mandatory { get; set; }
        }
    }
}