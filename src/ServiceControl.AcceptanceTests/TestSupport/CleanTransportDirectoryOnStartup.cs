using System.IO;
using NUnit.Framework;

[SetUpFixture]
public class CleanTransportDirectoryOnStartup
{
    [OneTimeSetUp]
    public void RemoveTransportDirectories()
    {
        var path = Path.Combine(Path.GetTempPath(), "ServiceControlTests", "TestTransport");

        if (Directory.Exists(path))
        {
            foreach (var subdirectory in Directory.GetDirectories(path))
            {
                try
                {
                    Directory.Delete(subdirectory, true);
                }
                catch (DirectoryNotFoundException)
                {
                }
            }
        }
    }
}