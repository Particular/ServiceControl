﻿using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
public abstract class FilterTestAttribute : Attribute, IApplyToContext
{
    public void ApplyToContext(TestExecutionContext context)
    {
        var currentFilters = Environment.GetEnvironmentVariable("TEST_FILTER")?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (!currentFilters.Contains(Filter))
        {
            Assert.Ignore("Ignoring because environment variable TEST_FILTER doesn't contain '{0}'.", Filter);
        }
    }

    protected abstract string Filter { get; }
}
