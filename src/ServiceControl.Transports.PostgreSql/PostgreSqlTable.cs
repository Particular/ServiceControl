#nullable enable
namespace ServiceControl.Transports.PostgreSql
{

    class PostgreSqlTable
    {
        //TODO postgres
        PostgreSqlTable(string name, string schema, string? catalog)
        {
            var quotedName = NameHelper.Quote(name);
            var quotedSchema = NameHelper.Quote(schema);
            //HINT: The query approximates queue length value based on max and min
            //      of RowVersion IDENTITY(1,1) column. There are couple of scenarios
            //      that might lead to the approximation being off. More details here:
            //      https://docs.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql-identity-property?view=sql-server-ver15#remarks
            //
            //      Min and Max values return NULL when no rows are found.
            if (catalog == null)
            {
                _fullTableName = $"{quotedSchema}.{quotedName}";

                LengthQuery = $"""
                               IF (EXISTS (SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{name}'))
                                 SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {_fullTableName} WITH (nolock)
                               ELSE
                                 SELECT -1;
                               """;
            }
            else
            {
                var quotedCatalog = NameHelper.Quote(catalog);
                _fullTableName = $"{quotedCatalog}.{quotedSchema}.{quotedName}";

                LengthQuery = $"""
                               IF (EXISTS (SELECT TABLE_NAME FROM {quotedCatalog}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{name}'))
                                 SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {_fullTableName} WITH (nolock)
                               ELSE
                                 SELECT -1;
                               """;
            }
        }

        readonly string _fullTableName;
        public string LengthQuery { get; }

        public override string ToString() =>
            _fullTableName;

        public static PostgreSqlTable Parse(string address, string defaultSchema)
        {
            var parts = address.Split('@');

            return new PostgreSqlTable(
                parts[0],
                parts.Length > 1 ? parts[1] : defaultSchema,
                parts.Length > 2 ? parts[2] : null
            );
        }

        protected bool Equals(PostgreSqlTable other) =>
            string.Equals(_fullTableName, other._fullTableName);

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((PostgreSqlTable)obj);
        }

        public override int GetHashCode() =>
            _fullTableName.GetHashCode();
    }
}