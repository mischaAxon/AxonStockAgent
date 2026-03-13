using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AxonStockAgent.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScanTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scan_triggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: true),
                    SignalsCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_triggers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scan_triggers_Status",
                table: "scan_triggers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_scan_triggers_CreatedAt",
                table: "scan_triggers",
                column: "CreatedAt");

            // Seed: scan.scan_source instelling (market_symbols is de nieuwe standaard)
            migrationBuilder.Sql(@"
                INSERT INTO algo_settings (category, key, value, description, value_type, min_value, max_value)
                VALUES ('scan', 'scan_source', 'market_symbols',
                        'Bron voor te scannen symbolen: ''market_symbols'' (alle actieve symbolen) of ''watchlist''',
                        'string', null, null)
                ON CONFLICT (category, key) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "scan_triggers");
            migrationBuilder.Sql("DELETE FROM algo_settings WHERE category = 'scan' AND key = 'scan_source'");
        }
    }
}
