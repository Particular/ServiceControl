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
            Assert.That(srcPath, Does.EndWith("src"));

            var repoRoot = Path.GetFullPath(Path.Combine(srcPath, ".."));

            // It would probably be more correct to parse csproj files but this is a good enough check for now.
            var paths = Directory.GetFiles(repoRoot, "ServiceControlInstaller.Engine.dll", SearchOption.AllDirectories)
                .Select(path => path.Substring(repoRoot.Length + 1))
                .Select(GetImportantPart)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var verifyText = string.Join(Environment.NewLine, paths);

            // ServiceControlInstaller.Engine.dll is quite large (100+ MB) because it contains embedded resources for each app,
            // transport package, persistence package, and RavenDB server. Introducing this dependency to other places in the 
            // build chain has big impacts on build speed and space available on disk during CI. Tests that need to use anything
            // in the Engine assembly should only be performed in these approved projects.
            Console.WriteLine(verifyText);
            Approver.Verify(verifyText);
        }

        string GetImportantPart(string path)
        {
            var binLocation = path.IndexOf($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");
            if (binLocation > 0)
            {
                path = path.Substring(0, binLocation);
            }


            var objLocation = path.IndexOf($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
            if (objLocation > 0)
            {
                path = path.Substring(0, objLocation);
            }

            return path.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}