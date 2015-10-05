namespace ServiceControlInstaller.Engine
{
    public interface ILogging
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
