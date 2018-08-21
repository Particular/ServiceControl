namespace ServiceControlInstaller.Engine.UnitTests
{
    using System;

    public class TestLogger : ILogging
    {
        public void Info(string message)
        {
            Console.WriteLine("INFO  : " + message);
        }

        public void Warn(string message)
        {
            Console.WriteLine("WARN  : " + message);
        }

        public void Error(string message)
        {
            Console.WriteLine("ERROR : " + message);
        }
    }
}