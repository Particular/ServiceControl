namespace ServiceControl.Federation.Api;

using System.Threading.Tasks;
using Members;
using Microsoft.AspNetCore.Http;

public delegate Task RouteHandler(Federator federator, HttpContext context);

/// <summary>
/// Strategy labels used in the route tables, matching the design document.
/// </summary>
public static class Strategy
{
    public const string Union = "U";
    public const string Sum = "S";
    public const string KeyMerge = "K";
    public const string SortedPage = "P";
    public const string TopN = "W";
    public const string FirstHit = "F";
    public const string RouteToOwner = "R";
    public const string Broadcast = "B";
    public const string Lead = "L";
    public const string Synthesised = "X";
}

/// <summary>
/// One proxied route: the methods and template it answers, relative to the surface prefix, and how.
/// </summary>
public sealed record FederatedRoute(Surface Surface, string[] Methods, string Template, string Strategy, RouteHandler Handler)
{
    /// <summary>
    /// Routes the members serve without authentication, such as the root document and the
    /// authentication configuration a client needs before it can sign in.
    /// </summary>
    public bool Anonymous { get; init; }
}
