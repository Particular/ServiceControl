namespace ServiceControl.Config
{
    using System.Reflection;
    using System.Windows;

    public class StandardPopup
    {
        public static void ApplyDefaultAlignment()
        {
            //HINT: this code enforces MenuDropAlignment to be default. This is important on laptops that enable `Tablet Mode`.
            //      In such case the dropdowns are moved to the left to not appear under hand when using touch. This behavior
            //      applies even if the users are not using touch and causes dropdowns to be misaligned. It's fine to override
            //      the setting at once at startup because the only way to change it by the users is via Control Panel.
            //      See: https://github.com/Particular/ServiceControl/issues/1374 for more details
            var dropdownAlignment = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);

            if (SystemParameters.MenuDropAlignment && dropdownAlignment != null)
            {
                dropdownAlignment.SetValue(null, false);
            }
        }
    }
}