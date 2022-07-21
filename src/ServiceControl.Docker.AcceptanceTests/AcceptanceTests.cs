namespace ServiceControl.Docker.AcceptanceTests
{
    using System;
    using System.IO;

    public class AcceptanceTests
    {
        public static string DockerFolder = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\docker"));
    }
}
