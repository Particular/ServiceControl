namespace ServiceControl.Hosting.Auth
{
    using System;
    using System.Security.Claims;

    public static class ClaimsPrinicpalExtensionMethods
    {
        public static string RequireClaim(this ClaimsPrincipal user, string claimType, string settingName)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"Authenticated principal is missing the required '{claimType}' claim configured by {settingName}. " +
                    "Configure the identity provider to emit this claim, or point the setting at the claim the IdP actually emits.");
            }
            return value;
        }
    }
}