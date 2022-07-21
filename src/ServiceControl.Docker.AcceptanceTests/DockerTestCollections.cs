namespace ServiceControl.Docker.AcceptanceTests
{
    using System.Collections;
    using System.IO;
    using System.Linq;

    public class DockerInitFilesCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            foreach (var dockerFile in Directory.GetFiles(AcceptanceTests.DockerFolder).Where(w => w.EndsWith("init-windows.dockerfile")))
            {
                yield return dockerFile;
            }
        }
    }

    public class DockerFilesCollection : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            foreach (var dockerFile in Directory.GetFiles(AcceptanceTests.DockerFolder).Where(w => w.StartsWith("servicecontrol")))
            {
                yield return dockerFile;
            }
        }
    }
}
