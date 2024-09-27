#nullable enable
namespace ServiceControl.Transports.PostgreSql;

class PostgreSqlTable
{
    public PostgreSqlTable(string name, string schema)
    {
        //HINT: The query approximates queue length value based on max and min of the table sequence.
        fullTableName = $"\"{schema}\".\"{name}\"";
        //NOTE: Postgres doesn't have NOLOCK since it utilises snapshot isolation by default
        LengthQuery = $$"""
                        SELECT CASE WHEN (EXISTS (SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{{schema}}' AND TABLE_NAME = '{{name}}')) THEN
                          COALESCE(cast(max(seq) - min(seq) + 1 AS int), 0)
                        ELSE
                          -1
                        END AS Id FROM {{fullTableName}};
                        """;
    }

    readonly string fullTableName;
    public string LengthQuery { get; }

    public override string ToString() =>
        fullTableName;

    bool Equals(PostgreSqlTable other) =>
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