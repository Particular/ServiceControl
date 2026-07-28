using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FullTextSearchSql.CreateCatalog, suppressTransaction: true);
            migrationBuilder.Sql(FullTextSearchSql.CreateIndex, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FullTextSearchSql.DropIndex, suppressTransaction: true);
            migrationBuilder.Sql(FullTextSearchSql.DropCatalog, suppressTransaction: true);
        }
    }
}
