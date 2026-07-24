using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ServiceControl.Persistence.EFCore.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "failing_endpoint_address",
                table: "failed_messages",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EndpointSettings",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    track_instances = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_endpoint_settings", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "trial_metadata",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trial_end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trial_metadata", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "trial_metadata",
                columns: new[] { "id", "trial_end_date" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "ix_failed_messages_failing_endpoint_address",
                table: "failed_messages",
                column: "failing_endpoint_address");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndpointSettings");

            migrationBuilder.DropTable(
                name: "trial_metadata");

            migrationBuilder.DropIndex(
                name: "ix_failed_messages_failing_endpoint_address",
                table: "failed_messages");

            migrationBuilder.DropColumn(
                name: "failing_endpoint_address",
                table: "failed_messages");
        }
    }
}
