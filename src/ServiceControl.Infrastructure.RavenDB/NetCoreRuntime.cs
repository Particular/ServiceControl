namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using NServiceBus.Logging;

    public class NetCoreRuntime
    {
        private NetCoreRuntime()
        {		
        }
	
        public string Runtime { get; private set; }
        public Version Version { get; private set; }
        public string Root { get; private set; }
	
        public static IEnumerable<NetCoreRuntime> FindAll()
        {
            try
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        FileName = @"dotnet",
                        Arguments = "--list-runtimes"
                    }
                };

                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(TimeToWaitInMs);

                return from line in output.Split('\n')
                    let match = Regex.Match(line, @"(.*)\s(\d+\.\d+\.\d+)\s\[(.*)\]")
                    where match.Success
                    select new NetCoreRuntime
                    {
                        Runtime = match.Result("$1"),
                        Version = Version.Parse(match.Result("$2")),
                        Root = match.Result("$3")
                    };
            }
            catch (Exception ex)
            {
                log.Error("Unable to find installed Net Core versions", ex);
                return Enumerable.Empty<NetCoreRuntime>();
            }
        }
        
        private const int TimeToWaitInMs = 1000;
        private static ILog log = LogManager.GetLogger<NetCoreRuntime>();
    }
}