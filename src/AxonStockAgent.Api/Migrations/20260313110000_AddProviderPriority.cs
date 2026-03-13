using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AxonStockAgent.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "data_providers",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            // Stel standaard prioriteiten in op basis van bekende volgorde:
            // EODHD (10) → Finnhub (20) → FMP (30) → overige (100)
            migrationBuilder.Sql("UPDATE data_providers SET \"Priority\" = 10 WHERE name = 'eodhd'");
            migrationBuilder.Sql("UPDATE data_providers SET \"Priority\" = 20 WHERE name = 'finnhub'");
            migrationBuilder.Sql("UPDATE data_providers SET \"Priority\" = 30 WHERE name = 'fmp'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "data_providers");
        }
    }
}
