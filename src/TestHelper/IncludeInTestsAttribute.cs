using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class IncludeInTestsAttribute : Attribute, IApplyToContext
{
    public void ApplyToContext(TestExecutionContext context)
    {
        var currentFilter = Environment.GetEnvironmentVariable("ServiceControl_TESTS_FILTER");
        context.OutWriter.WriteLine($"Environment variable ServiceControl_TESTS_FILTER is '{currentFilter ?? "not set"}'");
        if (currentFilter == null)
        {
            //allows running them all
            return;
        }

        if (currentFilter != Filter)
        {
            Assert.Ignore($"Ignoring because environment variable ServiceControl_TESTS_FILTER doesn't contain '{Filter}'.");
        }
    }

    protected abstract string Filter { get; }
}