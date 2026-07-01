#nullable enable
namespace ServiceControl.Infrastructure.Auth;

/// <summary>
/// The principal an audited action is attributed to. <see cref="Anonymous"/> is recorded when
/// authentication is disabled or no identified principal is present.
/// </summary>
public readonly record struct AuditUser(string Id, string Name)
{
    public const string AnonymousValue = "anonymous";

    public static readonly AuditUser Anonymous = new(AnonymousValue, AnonymousValue);
}
