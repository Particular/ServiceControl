namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;

    public class PlatformZipInfo
    {
        public static PlatformZipInfo Empty = new PlatformZipInfo(string.Empty, string.Empty, string.Empty, null, false);

        public PlatformZipInfo(string mainEntrypoint, string name, string filePath, Version version, bool present)
        {
            Name = name;
            MainEntrypoint = mainEntrypoint;
            FilePath = filePath;
            Version = version;
            Present = present;
        }

        public string MainEntrypoint { get; }
        public string Name { get; }
        public string FilePath { get; }
        public Version Version { get; }
        public bool Present { get; }
    }
}