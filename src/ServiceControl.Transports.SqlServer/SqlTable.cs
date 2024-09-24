#nullable enable
namespace ServiceControl.Transports.SqlServer
{

    class SqlTable
    {
        SqlTable(string name, string schema, string? catalog)
        {
            var unquotedSchema = SqlNameHelper.Unquote(schema);
            var unquotedName = SqlNameHelper.Unquote(name);
            var quotedName = SqlNameHelper.Quote(name);
            var quotedSchema = SqlNameHelper.Quote(schema);
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
                               IF (EXISTS (SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{unquotedSchema}' AND TABLE_NAME = '{unquotedName}'))
                                 SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {_fullTableName} WITH (nolock)
                               ELSE
                                 SELECT -1;
                               """;
            }
            else
            {
                var quotedCatalog = SqlNameHelper.Quote(catalog);
                _fullTableName = $"{quotedCatalog}.{quotedSchema}.{quotedName}";

                LengthQuery = $"""
                               IF (EXISTS (SELECT TABLE_NAME FROM {quotedCatalog}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{unquotedSchema}' AND TABLE_NAME = '{unquotedName}'))
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

        public static SqlTable Parse(string address, string defaultSchema)
        {
            var parts = address.Split('@');

            return new SqlTable(
                name: parts[0],
                schema: parts.Length > 1 ? parts[1] : defaultSchema,
                catalog: parts.Length > 2 ? parts[2] : null
            );
        }

        protected bool Equals(SqlTable other) =>
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

            return Equals((SqlTable)obj);
        }

        public override int GetHashCode() =>
            _fullTableName.GetHashCode();
    }
}