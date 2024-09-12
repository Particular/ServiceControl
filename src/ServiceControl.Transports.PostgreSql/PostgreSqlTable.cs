#nullable enable
namespace ServiceControl.Transports.PostgreSql
{

    class PostgreSqlTable
    {
        public PostgreSqlTable(string name, string schema)
        {
            //HINT: The query approximates queue length value based on max and min
            //      of RowVersion IDENTITY(1,1) column. There are couple of scenarios
            //      that might lead to the approximation being off. More details here:
            //      https://docs.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql-identity-property?view=sql-server-ver15#remarks
            //
            //      Min and Max values return NULL when no rows are found.
            fullTableName = $"{name}.{schema}";
            //TODO: Postgres should we add NOLOCK?
            LengthQuery = $$"""
                            IF (EXISTS (SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{{schema}}' AND TABLE_NAME = '{{name}}'))
                              SELECT COALESCE(cast(max(seq) - min(seq) + 1 AS int), 0) Id FROM {0}
                            ELSE
                              SELECT -1;
                            """;
        }

        readonly string fullTableName;
        public string LengthQuery { get; }

        public override string ToString() =>
            fullTableName;

        protected bool Equals(PostgreSqlTable other) =>
            string.Equals(fullTableName, other.fullTableName);

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
            fullTableName.GetHashCode();
    }
}