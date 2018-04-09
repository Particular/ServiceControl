namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Concurrent;
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
        public static IEnumerable<RunSummary> Run(IList<RunDescriptor> runDescriptors, IList<EndpointBehavior> behaviorDescriptors, IList<IScenarioVerification> shoulds, Func<ScenarioContext, bool> done, int limitTestParallelismTo, Action<RunSummary> reports, Func<Exception, bool> allowedExceptions)
        {
            var totalRuns = runDescriptors.Count();

            var cts = new CancellationTokenSource();

            var po = new ParallelOptions
            {
                CancellationToken = cts.Token
            };

            var maxParallelismSetting = Environment.GetEnvironmentVariable("max_test_parallelism");
            int maxParallelism;
            if (int.TryParse(maxParallelismSetting, out maxParallelism))
            {
                Console.Out.WriteLine($"Parallelism limited to: {maxParallelism}");

                po.MaxDegreeOfParallelism = maxParallelism;
            }

            if (limitTestParallelismTo > 0)
                po.MaxDegreeOfParallelism = limitTestParallelismTo;

            var results = new ConcurrentBag<RunSummary>();

            try
            {
                Parallel.ForEach(runDescriptors, po, runDescriptor =>
                {
                    if (po.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Console.Out.WriteLine($"{runDescriptor.Key} - Started @ {DateTime.Now}");

                    var runResult = PerformTestRun(behaviorDescriptors, shoulds, runDescriptor, done, allowedExceptions);

                    Console.Out.WriteLine($"{runDescriptor.Key} - Finished @ {DateTime.Now}");

                    results.Add(new RunSummary
                    {
                        Result = runResult,
                        RunDescriptor = runDescriptor,
                        Endpoints = behaviorDescriptors
                    });

                    if (runResult.Failed)
                    {
                        cts.Cancel();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Console.Out.WriteLine("Test run aborted due to test failures");
            }

            var failedRuns = results.Where(s => s.Result.Failed).ToList();

            foreach (var runSummary in failedRuns)
            {
                DisplayRunResult(runSummary, totalRuns);
            }

            if (failedRuns.Any())
                throw new AggregateException("Test run failed due to one or more exception", failedRuns.Select(f => f.Result.Exception));

            foreach (var runSummary in results.Where(s => !s.Result.Failed))
            {
                DisplayRunResult(runSummary, totalRuns);

                reports?.Invoke(runSummary);
            }

            return results;
        }

        static void DisplayRunResult(RunSummary summary, int totalRuns)
        {
            var runDescriptor = summary.RunDescriptor;
            var runResult = summary.Result;

            Console.Out.WriteLine("------------------------------------------------------");
            Console.Out.WriteLine($"Test summary for: {runDescriptor.Key}");
            if (totalRuns > 1)
                Console.Out.WriteLine($" - Permutation: {runDescriptor.Permutation}({totalRuns})");
            Console.Out.WriteLine(String.Empty);

            PrintSettings(runDescriptor.Settings);

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

        static RunResult PerformTestRun(IList<EndpointBehavior> behaviorDescriptors, IList<IScenarioVerification> shoulds, RunDescriptor runDescriptor, Func<ScenarioContext, bool> done, Func<Exception, bool> allowedExceptions)
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

                PerformScenarios(runDescriptor, runners, () =>
                {
                    if (!string.IsNullOrEmpty(runDescriptor.ScenarioContext.Exceptions))
                    {
                        var ex = new Exception(runDescriptor.ScenarioContext.Exceptions);
                        if (!allowedExceptions(ex))
                        {
                            throw new Exception("Failures in endpoints");
                        }
                    }
                    return done(runDescriptor.ScenarioContext);
                }).GetAwaiter().GetResult();

                runTimer.Stop();

                Parallel.ForEach(runners, runner =>
                {
                    foreach (var v in shoulds.Where(s => s.ContextType == runDescriptor.ScenarioContext.GetType()))
                    {
                        v.Verify(runDescriptor.ScenarioContext);
                    }
                });
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

        static async Task PerformScenarios(RunDescriptor runDescriptor, IEnumerable<ActiveRunner> runners, Func<bool> done)
        {
            var endpoints = runners.Select(r => r.Instance).ToList();
            try
            {
                StartEndpoints(endpoints);

                runDescriptor.ScenarioContext.EndpointsStarted = true;

                var maxTime = runDescriptor.TestExecutionTimeout;
                await ExecuteWhens(maxTime, endpoints).ConfigureAwait(false);

                var startTime = DateTime.UtcNow;
                while (!done())
                {
                    if (!Debugger.IsAttached)
                    {
                        if (DateTime.UtcNow - startTime > maxTime)
                        {
                            throw new ScenarioException(GenerateTestTimedOutMessage(maxTime));
                        }
                    }

                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
            finally
            {
                StopEndpoints(endpoints);
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

        static void StartEndpoints(IEnumerable<EndpointRunner> endpoints)
        {
            var tasks = endpoints.Select(endpoint => Task.Factory.StartNew(() =>
            {
                var result = endpoint.Start();

                if (result.Failed)
                {
                    throw new ScenarioException("Endpoint failed to start", result.Exception);
                }
            })).ToArray();

            if (!Task.WaitAll(tasks, TimeSpan.FromMinutes(2)))
            {
                throw new Exception("Starting endpoints took longer than 2 minutes");
            }
        }

        static void StopEndpoints(IEnumerable<EndpointRunner> endpoints)
        {
            var tasks = endpoints.Select(endpoint => Task.Factory.StartNew(() =>
            {
                Console.Out.WriteLine("Stopping endpoint: {0}", endpoint.Name());
                var sw = new Stopwatch();
                sw.Start();
                var result = endpoint.Stop();

                sw.Stop();
                if (result.Failed)
                    throw new ScenarioException("Endpoint failed to stop", result.Exception);

                Console.Out.WriteLine("Endpoint: {0} stopped ({1}s)", endpoint.Name(), sw.Elapsed);
            })).ToArray();

            if (!Task.WaitAll(tasks, TimeSpan.FromMinutes(2)))
                throw new Exception("Stopping endpoints took longer than 2 minutes");
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
}