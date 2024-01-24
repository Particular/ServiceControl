namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class Sequence<TContext>
        where TContext : ISequenceContext
    {
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
                await handler(context);
                return true;
            });
            stepNames.Add(step);
            return this;
        }

        public bool IsFinished(TContext context) => context.Step >= steps.Count;

        public async Task<bool> Continue(TContext context)
        {
            var currentStep = context.Step;
            if (currentStep >= steps.Count)
            {
                return true;
            }

            var step = steps[currentStep];
            var advance = await step(context);
            if (advance)
            {
                var nextStep = currentStep + 1;
                var finished = nextStep >= steps.Count;
                Console.WriteLine(finished
                    ? "Sequence finished"
                    : $"Advancing from {stepNames[currentStep]} to {stepNames[nextStep]}");

                context.Step = nextStep;
                return finished;
            }

            await Task.Delay(250);

            return false;
        }

        List<Func<TContext, Task<bool>>> steps = [];
        List<string> stepNames = [];
    }
}