using System;
using System.Linq;
using NUnit.Framework;
using Raven.TestDriver;
using ServiceControl.Infrastructure.RavenDB;

[SetUpFixture]
// ReSharper disable once CheckNamespace
public class InitializeRavenTestDriver
{
    [OneTimeSetUp]
    public void Initialize()
    {
        var highestUsableNetCoreRuntime = NetCoreRuntime.FindAll()
            .Where(x => x.Runtime == "Microsoft.NETCore.App")
            .Where(x => x.Version.Major == 3 && x.Version.Minor == 1)
            .OrderByDescending(x => x.Version)
            .FirstOrDefault() ?? throw new Exception("Could not find any .NET Core runtime 3.1.x");

        RavenTestDriver.ConfigureServer(new TestServerOptions
        {
            FrameworkVersion = highestUsableNetCoreRuntime.Version.ToString()
        });
    }
}