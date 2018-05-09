using System;

namespace DatabaseMigrationsTester
{
    using System.Threading;

    class Program
    {
        static int Main(string[] args)
        {
            var command = (Command)Enum.Parse(typeof(Command), args[0]);
            switch (command)
            {
                case Command.Throw:
                    throw new Exception("Boom!");
                case Command.Return0:
                    return 0;
                case Command.WriteToErrorAndExitNonZero:
                    Console.Error.WriteLine("Some error message");
                    return 1;
                case Command.WriteToErrorAndExitZero:
                    Console.Error.WriteLine("Some error message");
                    return 0;
                case Command.Timeout:
                    Thread.Sleep(100000);
                    return 0;
                case Command.UpdateProgress:
                    Console.Out.WriteLine("Updating schema from version: 1");
                    Console.Out.WriteLine("Updating schema from version: 2");
                    Console.Out.WriteLine("Updating schema from version: 3");
                    Console.Out.WriteLine("OK Upgrading");
                    return 0;
            }

            return 0;
        }
    }

    enum Command
    {
        Throw,
        Timeout,
        Return0,
        WriteToErrorAndExitNonZero,
        WriteToErrorAndExitZero,
        UpdateProgress,
    }
}

