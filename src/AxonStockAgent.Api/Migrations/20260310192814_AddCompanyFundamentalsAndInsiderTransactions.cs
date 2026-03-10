using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AxonStockAgent.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFundamentalsAndInsiderTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_fundamentals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    PeRatio = table.Column<double>(type: "double precision", nullable: true),
                    ForwardPe = table.Column<double>(type: "double precision", nullable: true),
                    PbRatio = table.Column<double>(type: "double precision", nullable: true),
                    PsRatio = table.Column<double>(type: "double precision", nullable: true),
                    EvToEbitda = table.Column<double>(type: "double precision", nullable: true),
                    ProfitMargin = table.Column<double>(type: "double precision", nullable: true),
                    OperatingMargin = table.Column<double>(type: "double precision", nullable: true),
                    ReturnOnEquity = table.Column<double>(type: "double precision", nullable: true),
                    ReturnOnAssets = table.Column<double>(type: "double precision", nullable: true),
                    RevenueGrowthYoy = table.Column<double>(type: "double precision", nullable: true),
                    EarningsGrowthYoy = table.Column<double>(type: "double precision", nullable: true),
                    DebtToEquity = table.Column<double>(type: "double precision", nullable: true),
                    CurrentRatio = table.Column<double>(type: "double precision", nullable: true),
                    QuickRatio = table.Column<double>(type: "double precision", nullable: true),
                    DividendYield = table.Column<double>(type: "double precision", nullable: true),
                    PayoutRatio = table.Column<double>(type: "double precision", nullable: true),
                    MarketCap = table.Column<double>(type: "double precision", nullable: true),
                    Revenue = table.Column<double>(type: "double precision", nullable: true),
                    NetIncome = table.Column<double>(type: "double precision", nullable: true),
                    SharesOutstanding = table.Column<long>(type: "bigint", nullable: true),
                    AnalystBuy = table.Column<int>(type: "integer", nullable: true),
                    AnalystHold = table.Column<int>(type: "integer", nullable: true),
                    AnalystSell = table.Column<int>(type: "integer", nullable: true),
                    AnalystStrongBuy = table.Column<int>(type: "integer", nullable: true),
                    AnalystStrongSell = table.Column<int>(type: "integer", nullable: true),
                    TargetPriceHigh = table.Column<double>(type: "double precision", nullable: true),
                    TargetPriceLow = table.Column<double>(type: "double precision", nullable: true),
                    TargetPriceMean = table.Column<double>(type: "double precision", nullable: true),
                    TargetPriceMedian = table.Column<double>(type: "double precision", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_fundamentals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "data_providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ApiKeyEncrypted = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: false),
                    SupportsEu = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsUs = table.Column<bool>(type: "boolean", nullable: false),
                    IsFree = table.Column<bool>(type: "boolean", nullable: false),
                    MonthlyCost = table.Column<decimal>(type: "numeric", nullable: false),
                    HealthStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "unknown"),
                    LastHealthCheck = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dividends",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    ExDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dividends", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "insider_transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Relation = table.Column<string>(type: "text", nullable: false),
                    TransactionType = table.Column<string>(type: "text", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Shares = table.Column<long>(type: "bigint", nullable: false),
                    PricePerShare = table.Column<double>(type: "double precision", nullable: false),
                    TotalValue = table.Column<double>(type: "double precision", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insider_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "news_articles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Symbol = table.Column<string>(type: "text", nullable: true),
                    Sector = table.Column<string>(type: "text", nullable: true),
                    SentimentScore = table.Column<double>(type: "double precision", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news_articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "portfolio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Shares = table.Column<int>(type: "integer", nullable: false),
                    AvgBuyPrice = table.Column<double>(type: "double precision", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sector_sentiment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Sector = table.Column<string>(type: "text", nullable: false),
                    AvgSentiment = table.Column<double>(type: "double precision", nullable: false),
                    ArticleCount = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sector_sentiment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    TechScore = table.Column<double>(type: "double precision", nullable: false),
                    MlProbability = table.Column<float>(type: "real", nullable: true),
                    SentimentScore = table.Column<double>(type: "double precision", nullable: true),
                    ClaudeConfidence = table.Column<double>(type: "double precision", nullable: true),
                    ClaudeDirection = table.Column<string>(type: "text", nullable: true),
                    ClaudeReasoning = table.Column<string>(type: "text", nullable: true),
                    FinalScore = table.Column<double>(type: "double precision", nullable: false),
                    FinalVerdict = table.Column<string>(type: "text", nullable: false),
                    PriceAtSignal = table.Column<double>(type: "double precision", nullable: false),
                    TrendStatus = table.Column<string>(type: "text", nullable: true),
                    MomentumStatus = table.Column<string>(type: "text", nullable: true),
                    VolatilityStatus = table.Column<string>(type: "text", nullable: true),
                    VolumeStatus = table.Column<string>(type: "text", nullable: true),
                    Notified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "watchlist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Exchange = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Sector = table.Column<string>(type: "text", nullable: true),
                    Industry = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    MarketCap = table.Column<long>(type: "bigint", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    WebUrl = table.Column<string>(type: "text", nullable: true),
                    SectorSource = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watchlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_fundamentals_Symbol",
                table: "company_fundamentals",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_data_providers_Name",
                table: "data_providers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dividends_Symbol_ExDate",
                table: "dividends",
                columns: new[] { "Symbol", "ExDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_insider_transactions_Symbol",
                table: "insider_transactions",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_insider_transactions_Symbol_Name_TransactionDate",
                table: "insider_transactions",
                columns: new[] { "Symbol", "Name", "TransactionDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_insider_transactions_TransactionDate",
                table: "insider_transactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_PublishedAt",
                table: "news_articles",
                column: "PublishedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_Sector",
                table: "news_articles",
                column: "Sector");

            migrationBuilder.CreateIndex(
                name: "IX_news_articles_Symbol",
                table: "news_articles",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_portfolio_Symbol",
                table: "portfolio",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_Token",
                table: "refresh_tokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_sector_sentiment_Sector_CalculatedAt",
                table: "sector_sentiment",
                columns: new[] { "Sector", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_signals_CreatedAt",
                table: "signals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_signals_FinalVerdict",
                table: "signals",
                column: "FinalVerdict");

            migrationBuilder.CreateIndex(
                name: "IX_signals_Symbol",
                table: "signals",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_Sector",
                table: "watchlist",
                column: "Sector");

            migrationBuilder.CreateIndex(
                name: "IX_watchlist_Symbol",
                table: "watchlist",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_fundamentals");

            migrationBuilder.DropTable(
                name: "data_providers");

            migrationBuilder.DropTable(
                name: "dividends");

            migrationBuilder.DropTable(
                name: "insider_transactions");

            migrationBuilder.DropTable(
                name: "news_articles");

            migrationBuilder.DropTable(
                name: "portfolio");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "sector_sentiment");

            migrationBuilder.DropTable(
                name: "signals");

            migrationBuilder.DropTable(
                name: "watchlist");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
