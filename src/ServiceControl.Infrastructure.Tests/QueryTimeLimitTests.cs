namespace ServiceControl.Infrastructure.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Infrastructure;

[TestFixture]
public class QueryTimeLimitTests
{
    const string SettingName = "ServiceControl.Test/QueryTimeoutInSeconds";

    [Test]
    public async Task A_query_within_the_limit_returns_its_result()
    {
        var result = await QueryTimeLimit.Run(_ => Task.FromResult(42), TimeSpan.FromSeconds(30), SettingName, CancellationToken.None);

        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void A_query_over_the_limit_is_cancelled_and_reported_as_a_timeout_naming_the_setting()
    {
        var exception = Assert.ThrowsAsync<TimeoutException>(() =>
            QueryTimeLimit.Run(Hang, TimeSpan.FromMilliseconds(50), SettingName, CancellationToken.None));

        Assert.That(exception.Message, Does.Contain(SettingName));
        Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void An_exception_the_provider_raises_for_the_cancellation_is_still_reported_as_a_timeout()
    {
        // Microsoft.Data.SqlClient reports a cancelled command as SqlException("Operation cancelled by user"),
        // not as OperationCanceledException. Once the deadline has fired, whatever surfaces is the timeout.
        var exception = Assert.ThrowsAsync<TimeoutException>(() =>
            QueryTimeLimit.Run(HangThenFailLikeSqlClient, TimeSpan.FromMilliseconds(50), SettingName, CancellationToken.None));

        Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
        Assert.That(exception.InnerException.Message, Is.EqualTo("Operation cancelled by user."));
    }

    [Test]
    public void A_failure_before_the_deadline_is_not_a_timeout()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            QueryTimeLimit.Run<int>(_ => throw new InvalidOperationException("boom"), TimeSpan.FromSeconds(30), SettingName, CancellationToken.None));
    }

    [Test]
    public void Caller_cancellation_is_propagated_as_cancellation_not_as_a_timeout()
    {
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var exception = Assert.CatchAsync(() => QueryTimeLimit.Run(Hang, TimeSpan.FromSeconds(30), SettingName, caller.Token));

        Assert.That(exception, Is.InstanceOf<OperationCanceledException>());
        Assert.That(exception, Is.Not.InstanceOf<TimeoutException>());
    }

    [Test]
    public void Caller_cancellation_that_the_provider_reports_as_its_own_error_is_still_cancellation()
    {
        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var exception = Assert.CatchAsync(() => QueryTimeLimit.Run(HangThenFailLikeSqlClient, TimeSpan.FromSeconds(30), SettingName, caller.Token));

        Assert.That(exception, Is.InstanceOf<OperationCanceledException>());
        Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
    }

    [TestCase(60, 60)]
    [TestCase(300, 300)]
    [TestCase(3600, 3600)]
    public void The_configured_seconds_are_used_when_inside_the_allowed_range(int configured, int expectedSeconds)
    {
        var limit = QueryTimeLimit.Validate(configured, SettingName, NullLogger.Instance);

        Assert.That(limit, Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
    }

    [TestCase(0)]
    [TestCase(-5)]
    [TestCase(3601)]
    public void Values_outside_the_allowed_range_fall_back_to_the_default(int configured)
    {
        var logger = new RecordingLogger();

        var limit = QueryTimeLimit.Validate(configured, SettingName, logger);

        Assert.That(limit, Is.EqualTo(TimeSpan.FromSeconds(QueryTimeLimit.DefaultSeconds)));
        Assert.That(logger.Errors, Has.Count.EqualTo(1));
        Assert.That(logger.Errors[0], Does.Contain(SettingName));
    }

    static async Task<int> Hang(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 0;
    }

    static async Task<int> HangThenFailLikeSqlClient(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Operation cancelled by user.");
        }

        return 0;
    }

    class RecordingLogger : ILogger
    {
        public System.Collections.Generic.List<string> Errors { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
