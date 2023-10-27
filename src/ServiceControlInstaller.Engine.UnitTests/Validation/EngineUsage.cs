namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;

    public class EngineUsage
    {
        [Test]
        public void AvoidImproperDependencyOnEngineAssembly()
        {
            var srcPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            StringAssert.EndsWith("src", srcPath);

            var paths = Directory.GetFiles(srcPath, "ServiceControlInstaller.Engine.dll", SearchOption.AllDirectories)
                .Select(path => path.Substring(srcPath.Length + 1).Split(Path.DirectorySeparatorChar).First()) // Get project directory
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var verifyText = string.Join(Environment.NewLine, paths);

            // ServiceControlInstaller.Engine.dll is quite large (100+ MB) because it contains embedded resources for each app,
            // transport package, persistence package, and RavenDB server. Introducing this dependency to other places in the 
            // build chain has big impacts on build speed and space available on disk during CI. Tests that need to use anything
            // in the Engine assembly should only be performed in these approved projects.
            Approver.Verify(verifyText);
        }
    }
}
