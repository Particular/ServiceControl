namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.DbContexts;

static class MigrationSqlDialectExtensions
{
    /// <summary>
    /// How many <typeparamref name="TEntity"/> rows fit in one of <see cref="IMigrationSqlDialect.InsertMissing{TEntity}"/>'s statements, sized from the same column list the statement chunks by.
    /// </summary>
    public static int RowsPerStatementFor<TEntity>(this IMigrationSqlDialect dialect, ServiceControlDbContext dbContext) where TEntity : class =>
        dialect.RowsPerStatement(MigrationInsert<TEntity>.For(dbContext).Columns.Count);
}
