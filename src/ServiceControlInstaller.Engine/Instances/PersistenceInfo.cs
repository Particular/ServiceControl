namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;

    public class PersistenceManifest
    {
        public string Name { get; set; }
        public string ZipName { get; set; }
        public string TypeName { get; set; }

        public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}