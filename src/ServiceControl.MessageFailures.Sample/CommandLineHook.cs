namespace ServiceControl.MessageFailures.Sample
{
    using System;
    using System.Linq;
    using NServiceBus;

    class CommandLineHook : IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }

        public void Start()
        {
            while (true)
            {
                Console.Out.WriteLine("Commands F{number of messges to generate} , M{f|s} to set the handler to fail or succeed");


                var input = Console.ReadLine().ToLower();

                var command = input.First();

                var parameter = input.Substring(1);

                switch (command)
                {
                    case 'f':
                        var number = int.Parse(parameter);

                        Console.Out.WriteLine("Sending {0} messages that will {1}", number, MessageThatWillFailHandler.Succeed ? "succeed" : "fail");

                        for (var i = 0; i < number; i++)
                        {
                            Bus.SendLocal(new MessageThatWillFail());
                        }
                        break;
                    case 'm':
                        MessageThatWillFailHandler.Succeed = parameter == "s";

                        Console.Out.WriteLine("Mode changed to: {0}", MessageThatWillFailHandler.Succeed ? "succeed" : "fail");

                        break;
                    default:
                        throw new Exception("Invalid command: " + command);
                }


            }
        }

        public void Stop()
        {

        }
    }
}