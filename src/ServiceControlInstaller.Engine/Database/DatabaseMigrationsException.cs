namespace ServiceControlInstaller.Engine.Database
{
    using System;

    public class DatabaseMigrationsException : Exception
    {
        public DatabaseMigrationsException(string message) : base(message)
        {
        }
    }
}