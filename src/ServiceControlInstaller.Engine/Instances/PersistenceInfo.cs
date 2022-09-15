﻿namespace ServiceControlInstaller.Engine.Instances
{
    using System.Collections.Generic;

    public class PersistenceInfo
    {
        public string Name { get; set; }
        public string ZipName { get; set; }
        public string TypeName { get; set; }

        public IDictionary<string, string> Settings { get; set; }
    }
}