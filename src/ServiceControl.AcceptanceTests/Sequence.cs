namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class Sequence<TContext>
        where TContext : ISequenceContext
    {
        List<Func<TContext, Task<bool>>> steps = new List<Func<TContext, Task<bool>>>();
        List<string> stepNames = new List<string>();

        public Sequence<TContext> Do(string step, Func<TContext, Task<bool>> handler)
        {
            steps.Add(handler);
            stepNames.Add(step);
            return this;
        }

        public Sequence<TContext> Do(string step, Func<TContext, Task> handler)
        {
            steps.Add(async context =>
            {
                await handler(context).ConfigureAwait(false);
                return true;
            });
            stepNames.Add(step);
            return this;
        }

        public bool IsFinished(TContext context)
        {
            return context.Step >= steps.Count;
        }

        public async Task<bool> Continue(TContext context)
        {
            var currentStep = context.Step;
            if (currentStep >= steps.Count)
            {
                return true;
            }

            var step = steps[currentStep];
            var advance = await step(context).ConfigureAwait(false);
            if (advance)
            {
                var nextStep = currentStep + 1;
                var finished = nextStep >= steps.Count;
                if (finished)
                {
                    Console.WriteLine("Sequence finished");
                }
                else
                {
                    Console.WriteLine($"Advancing from {stepNames[currentStep]} to {stepNames[nextStep]}");
                }

                context.Step = nextStep;
                return finished;
            }
            return false;
        }
    }
}