namespace ServiceControl.AcceptanceTests.Security.OpenIdConnect
{
    using System.Collections.Generic;

    // Deserialization target for the my/permissions/all response. The controller's
    // PermissionsDescriptor exposes Permissions as IReadOnlySet<string>, which System.Text.Json
    // cannot deserialize (it can't instantiate an interface). The serialized JSON is a plain array,
    // so a concrete HashSet round-trips it.
    record PermissionsResponse(string User, HashSet<string> Permissions);
}
