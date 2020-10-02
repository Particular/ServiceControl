namespace ServiceControl.Transports.SqlServer
{
    using System;
    using System.Linq;

    class SqlTable
    {
        SqlTable(string name, string schema, string catalog)
        {
            QuotedName = SqlNameHelper.Quote(name);
            QuotedSchema = SqlNameHelper.Quote(schema);
            QuotedCatalog = SqlNameHelper.Quote(catalog);
        }

        public string QuotedName { get; }
        public string UnquotedName => SqlNameHelper.Unquote(QuotedName);

        public string QuotedSchema { get; }
        public string UnquotedSchema => SqlNameHelper.Unquote(QuotedSchema);

        public string QuotedCatalog { get; }
        public string UnquotedCatalog => SqlNameHelper.Unquote(QuotedCatalog);

        public override string ToString()
        {
            return QuotedCatalog != null ? $"{QuotedCatalog}.{QuotedSchema}.{QuotedName}" : $"{QuotedSchema}.{QuotedName}";
        }

        public static SqlTable Parse(string address, string defaultSchema)
        {
            var parts = address.Split('@').ToArray();

            return new SqlTable(
                parts[0],
                parts.Length > 1 ? parts[1] : defaultSchema,
                parts.Length > 2 ? parts[2] : null
            );
        }

        protected bool Equals(SqlTable other)
        {
            return string.Equals(QuotedName, other.QuotedName) && string.Equals(QuotedSchema, other.QuotedSchema) && string.Equals(QuotedCatalog, other.QuotedCatalog);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((SqlTable) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (QuotedName != null ? QuotedName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (QuotedSchema != null ? QuotedSchema.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (QuotedCatalog != null ? QuotedCatalog.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}