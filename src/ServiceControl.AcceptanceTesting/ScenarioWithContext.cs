namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Support;

    public class ScenarioWithContext<TContext> : IScenarioWithEndpointBehavior<TContext> where TContext : ScenarioContext, new()
    {
        public ScenarioWithContext(Func<TContext> factory)
        {
            contextFactory = factory;
        }

        public IScenarioWithEndpointBehavior<TContext> WithEndpoint<T>() where T : EndpointConfigurationBuilder
        {
            return WithEndpoint<T>(b => { });
        }

        public IScenarioWithEndpointBehavior<TContext> WithEndpoint<T>(Action<EndpointBehaviorBuilder<TContext>> defineBehavior) where T : EndpointConfigurationBuilder
        {
            var builder = new EndpointBehaviorBuilder<TContext>(typeof(T));

            defineBehavior(builder);

            behaviors.Add(builder.Build());

            return this;
        }

        public IScenarioWithEndpointBehavior<TContext> Done(Func<TContext, bool> func)
        {
            done = c => func((TContext)c);

            return this;
        }

        private async Task<TContext> Run(TimeSpan? testExecutionTimeout = null)
        {
            var runDescriptor = new RunDescriptor
            {
                Key = "Default",
                ScenarioContext = contextFactory(),
                TestExecutionTimeout = testExecutionTimeout ?? TimeSpan.FromSeconds(90)
            };

            var sw = new Stopwatch();

            sw.Start();
            await ScenarioRunner.Run(runDescriptor, behaviors, done).ConfigureAwait(false);

            sw.Stop();

            Console.Out.WriteLine("Total time for testrun: {0}", sw.Elapsed);

            return (TContext)runDescriptor.ScenarioContext;
        }

        TContext IScenarioWithEndpointBehavior<TContext>.Run(TimeSpan? testExecutionTimeout)
        {
            return Run(testExecutionTimeout).GetAwaiter().GetResult();
        }

        readonly IList<EndpointBehavior> behaviors = new List<EndpointBehavior>();
        private Func<ScenarioContext, bool> done = context => true;

        Func<TContext> contextFactory;
    }
}