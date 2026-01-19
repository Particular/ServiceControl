namespace ServiceControl.Hosting.Auth
{
    using Microsoft.AspNetCore.Builder;

    public static class WebApplicationExtensions
    {
        public static void UseServiceControlAuthentication(this WebApplication app, bool authenticationEnabled = false)
        {
            if (!authenticationEnabled)
            {
                return;
            }

            app.UseAuthentication();
            app.UseAuthorization();
        }
    }
}
