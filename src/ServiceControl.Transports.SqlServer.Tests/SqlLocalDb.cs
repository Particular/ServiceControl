﻿namespace ServiceControl.Transports.SqlServer.Tests
{
    public class SqlLocalDb
    {
        readonly string name;
        const string ConnectionTemplate =
            @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog={0};Integrated Security=True;Connection Timeout=60;";

        public static SqlLocalDb CreateNewIn(string path) => new SqlLocalDb(path);

        SqlLocalDb(string path)
        {
            name = $"Test_{System.IO.Path.GetRandomFileName().Replace(".", string.Empty)}";

            var connection = NewConnectionToMaster();
            using (connection)
            {
                connection.Open();

                var sql = $@"CREATE DATABASE[{name}]ON PRIMARY (NAME={name}, FILENAME = '{path}\{name}.mdf')
                                        LOG ON (NAME={name}_log, FILENAME = '{path}\{name}_sql_transport_log.ldf')";

                var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
        }

        public void Detach()
        {
            using (var connection = NewConnectionToMaster())
            {
                connection.Open();
                ExecuteSql(connection, $"ALTER DATABASE [{name}] SET OFFLINE WITH ROLLBACK IMMEDIATE");
                ExecuteSql(connection, $"exec sp_detach_db '{name}'");
            }
        }

        static Microsoft.Data.SqlClient.SqlConnection NewConnectionToMaster() => new Microsoft.Data.SqlClient.SqlConnection(string.Format(ConnectionTemplate, "master"));

        Microsoft.Data.SqlClient.SqlConnection NewConnection() => new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);

        public int ExecuteSql(System.Data.IDbConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandType = System.Data.CommandType.Text;
                return command.ExecuteNonQuery();
            }
        }

        public int ExecuteScalar(System.Data.IDbConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandType = System.Data.CommandType.Text;
                return (int)command.ExecuteScalar();
            }
        }

        public string ConnectionString => string.Format(ConnectionTemplate, name);

        public bool? HasTableWithName(string tableName)
        {
            using (var connection = NewConnection())
            {
                connection.Open();
                var result = ExecuteScalar(connection, $"IF OBJECT_ID (N'[{tableName}]', N'U') IS NOT NULL SELECT 1 AS r ELSE SELECT 0 AS r;");
                return result == 1;
            }
        }
    }
}
