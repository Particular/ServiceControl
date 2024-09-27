namespace ServiceControl.Transports.PostgreSql;

using System;

// NOTE: Copied from the SQL Transport
public class QueueAddress
{
    public QueueAddress(string table, string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        Table = SafeUnquote(table);
        Schema = SafeUnquote(schemaName);
        QualifiedTableName = $"{PostgreSqlNameHelper.Quote(Schema)}.{PostgreSqlNameHelper.Quote(Table)}";
    }

    public string Table { get; }
    public string Schema { get; }
    public string QualifiedTableName { get; }

    public static QueueAddress Parse(string address)
    {
        var index = 0;
        var quoteCount = 0;
        while (index < address.Length)
        {
            if (address[index] == '"')
            {
                quoteCount++;
            }
            else if (address[index] == '.' && quoteCount % 2 == 0)
            {
                var schema = address.Substring(0, index);
                var table = address.Substring(index + 1);

                return new QueueAddress(table, schema);
            }
            index++;
        }

        return new QueueAddress(address, null);
    }

    static string SafeUnquote(string name)
    {
        var result = PostgreSqlNameHelper.Unquote(name);
        return string.IsNullOrWhiteSpace(result)
            ? null
            : result;
    }
}