namespace ServiceControl.Persistence.Infrastructure
{
    using System.Linq;
    using System.Security.Claims;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class AuthorizationInfo
    {
        public string[] ReadQueues { get; set; }
        public string[] WriteQueues { get; set; }
        public string[] HeaderFilters { get; set; }

        public bool IsQueueReadable(string queueAddress)
        {
            if (ReadQueues == null || ReadQueues.Length == 0 || ReadQueues.Contains("*"))
            {
                return true;
            }

            var normalized = queueAddress?.ToLowerInvariant();
            foreach (var queue in ReadQueues)
            {
                if (queue.Contains('*'))
                {
                    var pattern = "^" + string.Join(".*", queue.ToLowerInvariant().Split('*').Select(Regex.Escape)) + "$";
                    if (Regex.IsMatch(normalized ?? string.Empty, pattern))
                    {
                        return true;
                    }
                }
                else if (queue.ToLowerInvariant() == normalized)
                {
                    return true;
                }
            }

            return false;
        }

        public static AuthorizationInfo FromClaims(ClaimsPrincipal user)
        {
            user.FindFirst("ServicePlatformPermissions")
            var value = user.FindFirst("CanReadFromQueues")?.Value;
            var headerFiltersClaim = user.FindFirst("HeaderFilters")?.Value;

            //var headerFilters = JsonSerializer.Deserialize<string[]>(headerFiltersClaim);

            var authInfo = new AuthorizationInfo();
            if (!string.IsNullOrEmpty(value))
            {
                authInfo.ReadQueues = JsonSerializer.Deserialize<string[]>(value);
            }
            return authInfo;
        }
    }
}