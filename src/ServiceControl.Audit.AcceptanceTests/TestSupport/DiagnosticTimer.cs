namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Diagnostics;

    public class DiagnosticTimer : IDisposable
    {
        public DiagnosticTimer(string heading)
        {
            this.heading = heading;
            watch.Start();
        }

        public void Dispose()
        {
            watch.Stop();
            Console.Out.WriteLine($"{watch.Elapsed} - {heading}");
            GC.SuppressFinalize(this);
        }

        readonly string heading;
        Stopwatch watch = new Stopwatch();
    }
}