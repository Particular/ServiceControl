namespace ServiceControl.UnitTests.Infrastructure
{
    using System.IO;
    using System.Text.RegularExpressions;
    using NUnit.Framework;

    public class DockerfileTests
    {
        [Test]
        public void DontUseRavenArgs()
        {
            var srcDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            var ravenContainerDockerfilePath = Path.Combine(srcDir, "ServiceControl.RavenDB", "Dockerfile");
            var contents = File.ReadAllText(ravenContainerDockerfilePath);
            var setsRavenArgs = Regex.IsMatch(contents, @"RAVEN_ARGS=", RegexOptions.IgnoreCase);

            Assert.That(setsRavenArgs, Is.False, "Our Dockerfile should not set RAVEN_ARGS to anything, and leave that entirely to the end user. Instead, replace any --Setting.Name=value with an env var RAVEN_Setting_Name=value.");
        }
    }
}