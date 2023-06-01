﻿using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class IncludeInTestsAttribute : Attribute, IApplyToContext
{
    public void ApplyToContext(TestExecutionContext context)
    {
        var currentFilter = Environment.GetEnvironmentVariable("TEST_FILTER");
        context.OutWriter.WriteLine($"Environment variable TEST_FILTER is '{currentFilter ?? "null or not set"}'");
        if (currentFilter == "All")
        {
            return;
        }

        if (currentFilter != Filter)
        {
            Assert.Ignore($"Ignoring because environment variable TEST_FILTER doesn't contain '{Filter}'.");
        }
    }

    protected abstract string Filter { get; }
}
