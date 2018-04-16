namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Customization;

    public class ScenarioRunner
    {
        public static async Task Run(RunDescriptor runDescriptor, IList<EndpointBehavior> behaviorDescriptors, Func<ScenarioContext, Task<bool>> done)
        {
            Console.Out.WriteLine($"{runDescriptor.Key} - Started @ {DateTime.Now}");

            var runResult = await PerformTestRun(behaviorDescriptors, runDescriptor, done)
                .ConfigureAwait(false);

            Console.Out.WriteLine($"{runDescriptor.Key} - Finished @ {DateTime.Now}");

            var result = new RunSummary
            {
                Result = runResult,
                RunDescriptor = runDescriptor,
                Endpoints = behaviorDescriptors
            };

            DisplayRunResult(result);

            if (result.Result.Failed)
            {
                throw new Exception("Test run failed due to one or more exception", result.Result.Exception);
            }
        }

        static void DisplayRunResult(RunSummary summary)
        {
            var runDescriptor = summary.RunDescriptor;
            var runResult = summary.Result;

            Console.Out.WriteLine("------------------------------------------------------");
            Console.Out.WriteLine($"Test summary for: {runDescriptor.Key}");
            Console.Out.WriteLine(String.Empty);

            Console.WriteLine(String.Empty);
            Console.WriteLine("Endpoints:");

            foreach (var endpoint in runResult.ActiveEndpoints)
            {
                Console.Out.WriteLine("     - {0}", endpoint);
            }

            if (runResult.Failed)
            {
                Console.Out.WriteLine($"Test failed: {runResult.Exception}");
            }
            else
            {
                Console.Out.WriteLine($"Result: Successful - Duration: {runResult.TotalTime}");
            }

            //dump trace and context regardless since asserts outside the should could still fail the test
            Console.WriteLine(String.Empty);
            Console.Out.WriteLine("Context:");

            foreach (var prop in runResult.ScenarioContext.GetType().GetProperties())
            {
                Console.Out.WriteLine($"{prop.Name} = {prop.GetValue(runResult.ScenarioContext, null)}");
            }

            Console.Out.WriteLine("------------------------------------------------------");
        }

        static async Task<RunResult> PerformTestRun(IList<EndpointBehavior> behaviorDescriptors, RunDescriptor runDescriptor, Func<ScenarioContext, Task<bool>> done)
        {
            var runResult = new RunResult
            {
                ScenarioContext = runDescriptor.ScenarioContext
            };

            var runTimer = new Stopwatch();

            runTimer.Start();

            try
            {
                var runners = InitializeRunners(runDescriptor, behaviorDescriptors);

                runResult.ActiveEndpoints = runners.Select(r => r.EndpointName).ToList();

                await PerformScenarios(runDescriptor, runners, () => done(runDescriptor.ScenarioContext)).ConfigureAwait(false);

                runTimer.Stop();
            }
            catch (Exception ex)
            {
                runResult.Failed = true;
                runResult.Exception = ex;
            }

            runResult.TotalTime = runTimer.Elapsed;

            return runResult;
        }

        static IDictionary<Type, string> CreateRoutingTable(IEnumerable<EndpointBehavior> behaviorDescriptors)
        {
            var routingTable = new Dictionary<Type, string>();

            foreach (var behaviorDescriptor in behaviorDescriptors)
            {
                routingTable[behaviorDescriptor.EndpointBuilderType] = GetEndpointNameForRun(behaviorDescriptor);
            }

            return routingTable;
        }

        private static void PrintSettings(IEnumerable<KeyValuePair<string, string>> settings)
        {
            Console.WriteLine(String.Empty);
            Console.WriteLine("Using settings:");
            foreach (var pair in settings)
            {
                Console.Out.WriteLine($"   {pair.Key}: {pair.Value}");
            }
            Console.WriteLine();
        }

        static async Task PerformScenarios(RunDescriptor runDescriptor, IEnumerable<ActiveRunner> runners, Func<Task<bool>> done)
        {
            var endpoints = runners.Select(r => r.Instance).ToList();
            try
            {
                await StartEndpoints(endpoints).ConfigureAwait(false);

                runDescriptor.ScenarioContext.EndpointsStarted = true;

                var maxTime = runDescriptor.TestExecutionTimeout;
                await ExecuteWhens(maxTime, endpoints).ConfigureAwait(false);

                var startTime = DateTime.UtcNow;
                while (!await done().ConfigureAwait(false))
                {
                    if (!Debugger.IsAttached)
                    {
                        if (DateTime.UtcNow - startTime > maxTime)
                        {
                            throw new ScenarioException(GenerateTestTimedOutMessage(maxTime));
                        }
                    }

                    await Task.Delay(500).ConfigureAwait(false); // slow down to prevent hammering of SC APIs
                    await Task.Yield(); // yield to give some freedom
                }
            }
            finally
            {
                await StopEndpoints(endpoints).ConfigureAwait(false);
            }
        }

        private static async Task ExecuteWhens(TimeSpan maxTime, List<EndpointRunner> endpoints)
        {
            var tasks = endpoints.Select(endpoint =>
            {
                try
                {
                    return endpoint.ExecuteWhens();
                }
                catch (Exception ex)
                {
                    throw new ScenarioException("Whens failed to execute", ex);
                }
            });

            var whenAll = Task.WhenAll(tasks);
            var timeoutTask = Task.Delay(maxTime);
            var completedTask = await Task.WhenAny(whenAll, timeoutTask).ConfigureAwait(false);

            if (completedTask.Equals(timeoutTask))
            {
                throw new ScenarioException($"Executing whens took longer than {maxTime.TotalSeconds} seconds.");
            }

            if (completedTask.IsFaulted && completedTask.Exception != null)
            {
                ExceptionDispatchInfo.Capture(completedTask.Exception).Throw();
            }
        }

        static string GenerateTestTimedOutMessage(TimeSpan maxTime)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"The maximum time limit for this test({maxTime.TotalSeconds}s) has been reached");
            sb.AppendLine("----------------------------------------------------------------------------");

            return sb.ToString();
        }

        static Task StartEndpoints(IEnumerable<EndpointRunner> endpoints)
        {
            return endpoints.Select(endpoint => Task.Run(() =>
            {
                var result = endpoint.Start();

                if (result.Failed)
                {
                    throw new ScenarioException("Endpoint failed to start", result.Exception);
                }
            })).Timebox(TimeSpan.FromMinutes(2), "Starting endpoints took longer than 2 minutes");
        }

        static Task StopEndpoints(IEnumerable<EndpointRunner> endpoints)
        {
            return endpoints.Select(endpoint => Task.Run(() =>
            {
                Console.Out.WriteLine("Stopping endpoint: {0}", endpoint.Name());
                var sw = new Stopwatch();
                sw.Start();
                var result = endpoint.Stop();

                sw.Stop();
                if (result.Failed)
                    throw new ScenarioException("Endpoint failed to stop", result.Exception);

                Console.Out.WriteLine("Endpoint: {0} stopped ({1}s)", endpoint.Name(), sw.Elapsed);
            })).Timebox(TimeSpan.FromMinutes(2), "Stopping endpoints took longer than 2 minutes");
        }

        static List<ActiveRunner> InitializeRunners(RunDescriptor runDescriptor, IList<EndpointBehavior> behaviorDescriptors)
        {
            var runners = new List<ActiveRunner>();
            var routingTable = CreateRoutingTable(behaviorDescriptors);

            foreach (var behaviorDescriptor in behaviorDescriptors)
            {
                var endpointName = GetEndpointNameForRun(behaviorDescriptor);

                if (endpointName.Length > 77)
                {
                    throw new Exception($"Endpoint name '{endpointName}' is larger than 77 characters and will cause issues with MSMQ queue names. Please rename your test class or endpoint!");
                }

                var runner = PrepareRunner(endpointName);
                var result = runner.Instance.Initialize(runDescriptor, behaviorDescriptor, routingTable, endpointName);

                if (result.Failed)
                {
                    throw new ScenarioException($"Endpoint {runner.Instance.Name()} failed to initialize", result.Exception);
                }

                runners.Add(runner);
            }

            return runners;
        }

        static string GetEndpointNameForRun(EndpointBehavior endpointBehavior)
        {
            return Conventions.EndpointNamingConvention(endpointBehavior.EndpointBuilderType);
        }

        static ActiveRunner PrepareRunner(string endpointName)
        {
            return new ActiveRunner
            {
                Instance = new EndpointRunner(),
                EndpointName = endpointName
            };
        }
    }

    public class RunResult
    {
        public bool Failed { get; set; }

        public Exception Exception { get; set; }

        public TimeSpan TotalTime { get; set; }

        public ScenarioContext ScenarioContext { get; set; }

        public IEnumerable<string> ActiveEndpoints
        {
            get
            {
                if (activeEndpoints == null)
                    activeEndpoints = new List<string>();

                return activeEndpoints;
            }
            set { activeEndpoints = value.ToList(); }
        }

        IList<string> activeEndpoints;
    }

    public class RunSummary
    {
        public RunResult Result { get; set; }

        public RunDescriptor RunDescriptor { get; set; }

        public IEnumerable<EndpointBehavior> Endpoints { get; set; }
    }

    static class TaskExtensions
    {
        //this method will not timeout a task if the debugger is attached.
        public static Task Timebox(this IEnumerable<Task> tasks, TimeSpan timeoutAfter, string messageWhenTimeboxReached)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();
            var tokenSource = Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(timeoutAfter);
            var registration = tokenSource.Token.Register(s =>
            {
                var tcs = (TaskCompletionSource<object>)s;
                tcs.TrySetException(new TimeoutException(messageWhenTimeboxReached));
            }, taskCompletionSource);

            Task.WhenAll(tasks)
                .ContinueWith((t, s) =>
                {
                    var state = (Tuple<TaskCompletionSource<object>, CancellationTokenSource, CancellationTokenRegistration>)s;
                    var source = state.Item2;
                    var reg = state.Item3;
                    var tcs = state.Item1;

                    if (t.IsFaulted && t.Exception != null)
                    {
                        tcs.TrySetException(t.Exception.GetBaseException());
                    }

                    if (t.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }

                    if (t.IsCompleted)
                    {
                        tcs.TrySetResult(null);
                    }

                    reg.Dispose();
                    source.Dispose();
                }, Tuple.Create(taskCompletionSource, tokenSource, registration), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return taskCompletionSource.Task;
        }
    }
}