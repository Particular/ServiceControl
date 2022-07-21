namespace ServiceControl.Docker.AcceptanceTests
{
    using System.IO;
    using NUnit.Framework;
    using System.Linq;

    public class ConsistentContent
    {
        string expectedDotNet = "";

        [Test]
        [TestCaseSource(typeof(DockerInitFilesCollection))]
        public void Verify_files_have_same_dot_net_version(string currentDockerFile)
        {
            var dotNetLine = File.ReadLines(currentDockerFile).First(w => w.StartsWith("FROM mcr.microsoft.com/dotnet/framework/"));
            if (expectedDotNet == "")
            {
                expectedDotNet = dotNetLine;
            }
            else
            {
                Assert.AreEqual(expectedDotNet, dotNetLine);
            }

        }
        [Test]
        [TestCaseSource(typeof(DockerInitFilesCollection))]
        public void Verify_init_files_have_setup_flag(string currentDockerFile)
        {
            var setupLine = File.ReadLines(currentDockerFile).Last();
            Assert.IsTrue(setupLine.IndexOf("--setup") > -1);
        }
    }
}