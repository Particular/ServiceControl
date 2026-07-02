#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Auth;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
public class AuditServiceRegistrationTests
{
    [Test]
    public void Registers_message_action_audit_and_user_accessor()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(_ => { }));
        var settings = new OpenIdConnectSettings(new SettingsRootNamespace("ServiceControl"), validateConfiguration: false, requireServicePulseSettings: false);

        builder.AddServiceControlAuthorization(settings);

        using var provider = builder.Services.BuildServiceProvider();
        Assert.That(provider.GetService<IMessageActionAuditLog>(), Is.TypeOf<MessageActionAuditLog>());
        Assert.That(provider.GetService<ICurrentUserAccessor>(), Is.TypeOf<CurrentUserAccessor>());
    }
}
