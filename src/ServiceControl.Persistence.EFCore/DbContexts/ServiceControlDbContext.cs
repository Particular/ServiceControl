namespace ServiceControl.Persistence.EFCore.DbContexts;

using Microsoft.EntityFrameworkCore;

public abstract class ServiceControlDbContext(DbContextOptions options) : DbContext(options)
{
}
