namespace ServiceControl.Persistence.PerfTests
{
    using System.Threading.Tasks;
    using NBench;

    public class Program
    {
        public static int Main(string[] args)
        {
            return NBenchRunner.Run<Program>();
        }
    }
}