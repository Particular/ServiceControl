namespace ServiceControl.Config
{
    using System.Windows;

    public static class Splash
    {
        public static void Show()
        {
            var splash = new SplashScreen(typeof(Splash).Assembly, "Splash.png");
            splash.Show(true);
        }
    }
}