namespace ServiceControl.Infrastructure.Tests.StartupChecks;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Hosting;
using ServiceControl.Infrastructure.StartupChecks;

[TestFixture]
class StartupCheckHostedServiceTests
{
    List<string> events;

    [SetUp]
    public void SetUp() => events = [];

    [Test]
    public async Task Checks_run_in_order_in_one_scope_that_is_disposed_before_later_hosted_services()
    {
        using var host = Build(services =>
        {
            services.AddSingleton(events);
            services.AddScoped<ScopedDependency>();
            services.AddScoped<IStartupCheck>(p => Resolve(p, "first"));
            services.AddScoped<IStartupCheck>(p => Resolve(p, "second"));
        });

        await host.StartAsync();

        Assert.That(events, Is.EqualTo(new[] { "scope created", "first", "second", "scope disposed", "later service started" }));
    }

    [Test]
    public void A_failing_check_stops_startup_before_later_hosted_services()
    {
        using var host = Build(services =>
        {
            services.AddScoped<IStartupCheck>(_ => new ThrowingCheck());
            services.AddScoped<IStartupCheck>(_ => new RecordingCheck("never", events));
        });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync());

        Assert.That(ex.Message, Is.EqualTo("check failed"));
        Assert.That(events, Is.Empty);
    }

    [Test]
    public void Cancelling_the_start_token_cancels_the_running_check()
    {
        using var cts = new CancellationTokenSource();
        using var host = Build(services => services.AddScoped<IStartupCheck>(_ => new TokenCheck(t =>
        {
            cts.Cancel();
            t.ThrowIfCancellationRequested();
        })));

        Assert.CatchAsync<OperationCanceledException>(() => host.StartAsync(cts.Token));
        Assert.That(events, Is.Empty);
    }

    [Test]
    public async Task Startup_succeeds_without_checks()
    {
        using var host = Build(_ => { });

        await host.StartAsync();

        Assert.That(events, Is.EqualTo(new[] { "later service started" }));
    }

    RecordingCheck Resolve(IServiceProvider provider, string name)
    {
        provider.GetRequiredService<ScopedDependency>();
        return new RecordingCheck(name, events);
    }

    IHost Build(Action<IServiceCollection> configure)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        configure(builder.Services);
        builder.Services.AddStartupChecks();
        builder.Services.AddSingleton<IHostedService>(new LaterService(events));
        return builder.Build();
    }

    class ScopedDependency : IAsyncDisposable
    {
        readonly List<string> events;

        public ScopedDependency(List<string> events)
        {
            this.events = events;
            events.Add("scope created");
        }

        public ValueTask DisposeAsync()
        {
            events.Add("scope disposed");
            return ValueTask.CompletedTask;
        }
    }

    class RecordingCheck(string name, List<string> events) : IStartupCheck
    {
        public Task Check(CancellationToken cancellationToken = default)
        {
            events.Add(name);
            return Task.CompletedTask;
        }
    }

    class ThrowingCheck : IStartupCheck
    {
        public Task Check(CancellationToken cancellationToken = default) => throw new InvalidOperationException("check failed");
    }

    class TokenCheck(Action<CancellationToken> onCheck) : IStartupCheck
    {
        public Task Check(CancellationToken cancellationToken = default)
        {
            onCheck(cancellationToken);
            return Task.CompletedTask;
        }
    }

    class LaterService(List<string> events) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            events.Add("later service started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
