namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.DbContexts;
using Microsoft.EntityFrameworkCore;

public class SqlServerAuditDbContext : AuditDbContextBase
{
    public SqlServerAuditDbContext(DbContextOptions<SqlServerAuditDbContext> options) : base(options)
    {
    }
}
