namespace ServiceControl.Hosting.Auth
{
    using Microsoft.AspNetCore.Builder;

    public static class WebApplicationExtensions
    {
        public static void UseServiceControlAuthentication(this WebApplication app, bool authenticationEnabled = false)
        {
            if (authenticationEnabled)
            {
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapControllers().RequireAuthorization();
            }
            else
            {
                app.MapControllers();
            }
        }
    }
}
