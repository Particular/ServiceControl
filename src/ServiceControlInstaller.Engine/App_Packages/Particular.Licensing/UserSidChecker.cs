namespace Particular.Licensing
{
    using System.Security.Principal;

    static class UserSidChecker
    {
        public static bool IsNotSystemSid()
        {
            var windowsIdentity = WindowsIdentity.GetCurrent();
            return windowsIdentity?.User != null && !windowsIdentity.User.IsWellKnown(WellKnownSidType.LocalSystemSid);
        }
    }
}