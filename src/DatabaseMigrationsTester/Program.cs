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
    }
}

