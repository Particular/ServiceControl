namespace ServiceControl.Hosting
{
    using System.Reflection;

    public abstract class ServiceControlComponent
    {
        public Assembly GetAssembly() => GetType().Assembly;
    }
}
