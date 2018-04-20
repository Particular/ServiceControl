namespace ServiceControlInstaller.Engine.Database
{
	using System;
	public class DatabaseMigrationsTimeoutException : Exception
	{
		public DatabaseMigrationsTimeoutException(string message) : base(message)
		{

		}
	}
}
