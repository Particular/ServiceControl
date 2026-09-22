using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceControl.Persistence.EFCore.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FullTextSearchSql.CreateCatalog, suppressTransaction: true);
            migrationBuilder.Sql(FullTextSearchSql.CreateAuditKeyIndex, suppressTransaction: true);
            migrationBuilder.Sql(FullTextSearchSql.CreateAuditIndex, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FullTextSearchSql.DropAuditIndex, suppressTransaction: true);
            migrationBuilder.Sql(FullTextSearchSql.DropAuditKeyIndex, suppressTransaction: true);
            migrationBuilder.Sql(FullTextSearchSql.DropCatalog, suppressTransaction: true);
        }
    }
}
