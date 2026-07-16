namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.DbContexts;

public class PostgreSqlServiceControlDbContext(DbContextOptions<PostgreSqlServiceControlDbContext> options) : ServiceControlDbContext(options)
{
}
