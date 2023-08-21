namespace TestDataGenerator
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;

    public class Program
    {
        const int MaxBodySizeToStoreDefault = 102400;
        public const int EndpointCount = 6;

        static EndpointInfo sender;
        static EndpointInfo[] endpoints;
        static string commandResult;
        static bool running = true;
        static Random random = new Random();
        static readonly byte[] Data = new byte[MaxBodySizeToStoreDefault + 1]; // Gets serialized so will be larger than storage default so +1 isn't needed but just to indicate we w

        static async Task Main()
        {
            random.NextBytes(Data); // Initialize with random data

            // Want the endpoints to be largely silent
            LogManager.Use<DefaultFactory>().Level(LogLevel.Fatal);

            endpoints = Enumerable.Range(0, EndpointCount)
                .Select(i => new EndpointInfo($"Endpoint{i}"))
                .ToArray();

            sender = new EndpointInfo("Sender", sendOnly: true);

            await Task.WhenAll(endpoints.Select(e => e.Start()));
            await sender.Start();

            await RunLoop();

            await sender.Stop();
            await Task.WhenAll(endpoints.Select(e => e.Stop()));
        }

        static async Task RunLoop()
        {
            while (running)
            {
                Console.CursorVisible = false;
                Console.Clear();
                if (commandResult != null)
                {
                    Console.WriteLine(commandResult);
                    Console.WriteLine();
                    commandResult = null;
                }

                Console.WriteLine($"{"Name",-10}  {"Status",-7}  {"Msgs Received",12}  Flags");
                Console.WriteLine($"{"----",-10}  {"------",-7}  {"-------------",12}  ------------------");
                foreach (var endpoint in endpoints)
                {
                    Console.WriteLine(endpoint);
                }
                Console.WriteLine();

                void WriteCommand(string cmd, string description)
                {
                    Console.WriteLine($"{cmd,-20} {description}");
                }

                WriteCommand("Command", "Description");
                WriteCommand("-------", "-----------");
                WriteCommand("stop 1", "Stop Endpoint1");
                WriteCommand("start 1", "Start Endpoint1");
                WriteCommand("fanout", "Tests audit/visualizations");
                WriteCommand("saga-audits 1", "Create saga audit data on Endpoint1");
                WriteCommand("check-fail 1", "Set custom checks on Endpoint1 to fail");
                WriteCommand("check-pass 1", "Set custom checks on Endpoint1 to pass");
                WriteCommand("send 1 100", "Send 100 simple messages with various sizes to Endpoint1");
                WriteCommand("throw 1", "Set Endpoint1 to throw exceptions");
                WriteCommand("recover 1", "Set Endpoint1 to no longer throw exceptions");

                WriteCommand("-", "-");
                WriteCommand("q|quit|exit", "Quit");

                Console.WriteLine();
                Console.Write("> ");

                Console.CursorVisible = true;
                var input = Console.ReadLine();
                await ProcessCommand(input);
            }
        }

        static async Task ProcessCommand(string input)
        {
            var split = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var command = split.FirstOrDefault();
            var args = split.Skip(1).ToArray();

            if (string.IsNullOrEmpty(command))
            {
                // Just a refresh
                return;
            }

            string GetArg(int index) => (index < args.Length) ? args[index] : null;

            // Commands that don't require endpoint index
            switch (command)
            {
                case "fanout":
                    await RunFanout();
                    return;
                case "q":
                case "quit":
                case "exit":
                    running = false;
                    return;
                default:
                    break;
            }

            if (int.TryParse(GetArg(0), out var endpointIndex))
            {
                switch (command)
                {
                    case "start":
                        await StartEndpoint(endpointIndex);
                        return;
                    case "stop":
                        await StopEndpoint(endpointIndex);
                        return;
                    case "saga-audits":
                        await RunSagaAudit(endpointIndex);
                        return;
                    case "check-fail":
                        SetCustomChecks(endpointIndex, true);
                        return;
                    case "check-pass":
                        SetCustomChecks(endpointIndex, false);
                        return;
                    case "throw":
                        SetExceptionThrowing(endpointIndex, true);
                        return;
                    case "recover":
                        SetExceptionThrowing(endpointIndex, false);
                        return;
                    case "send":
                        if (int.TryParse(GetArg(1), out var count))
                        {
                            await SendSimpleMessages(endpointIndex, count);
                            return;
                        }
                        break;
                    default:
                        break;
                }
            }

            commandResult = "Invalid command: " + input;
        }

        static async Task RunFanout()
        {
            await sender.Instance.Send("Endpoint0", new FanoutCommand { Level = 0, Index = 0 });
        }

        static async Task RunSagaAudit(int endpoint)
        {
            var destination = $"Endpoint{endpoint}";
            string correlation = Guid.NewGuid().ToString().Substring(0, 8);
            await sender.Instance.Send(destination, new SagaMessage1 { CorrelationId = correlation });
            await sender.Instance.Send(destination, new SagaMessage2 { CorrelationId = correlation });
        }

        static async Task SendSimpleMessages(int endpoint, int count)
        {
            var destination = $"Endpoint{endpoint}";
            var tasks = Enumerable.Range(0, count)
                .Select(i => new SimpleCommand { Index = i, Data = random.Next(2) == 0 ? null : Data })
                .Select(msg => sender.Instance.Send(destination, msg));

            await Task.WhenAll(tasks);
        }

        static Task StopEndpoint(int endpointIndex) => endpoints[endpointIndex].Stop();
        static Task StartEndpoint(int endpointIndex) => endpoints[endpointIndex].Start();
        static void SetCustomChecks(int endpointIndex, bool shouldFail) => endpoints[endpointIndex].Context.FailCustomCheck = shouldFail;
        static void SetExceptionThrowing(int endpointIndex, bool shouldThrow) => endpoints[endpointIndex].Context.ThrowExceptions = shouldThrow;
    }
}