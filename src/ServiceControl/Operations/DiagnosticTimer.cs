namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;

    public class DiagnosticTimer : IDisposable
    {
        private readonly string heading;
        private Stopwatch watch = new Stopwatch();

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
    }
}