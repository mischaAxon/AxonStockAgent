using Microsoft.EntityFrameworkCore;
using AxonStockAgent.Api.Data.Entities;

namespace AxonStockAgent.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<WatchlistItem> Watchlist => Set<WatchlistItem>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();
    public DbSet<PortfolioItem> Portfolio => Set<PortfolioItem>();
    public DbSet<DividendEntity> Dividends => Set<DividendEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<DataProviderEntity> DataProviders => Set<DataProviderEntity>();
    public DbSet<NewsArticleEntity> NewsArticles { get; set; } = null!;
    public DbSet<SectorSentimentEntity> SectorSentiment { get; set; } = null!;
    public DbSet<CompanyFundamentalsEntity> CompanyFundamentals => Set<CompanyFundamentalsEntity>();
    public DbSet<InsiderTransactionEntity> InsiderTransactions => Set<InsiderTransactionEntity>();
    public DbSet<AlgoSettingsEntity> AlgoSettings => Set<AlgoSettingsEntity>();
    public DbSet<ClaudeApiLogEntity> ClaudeApiLogs => Set<ClaudeApiLogEntity>();
    public DbSet<MarketSymbolEntity> MarketSymbols { get; set; }
    public DbSet<TrackedExchangeEntity> TrackedExchanges { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchlistItem>(e =>
        {
            e.ToTable("watchlist");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol).IsUnique();
            e.HasIndex(x => x.Sector); // voor sector filtering
            e.Property(x => x.AddedAt).HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<SignalEntity>(e =>
        {
            e.ToTable("signals");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.FinalVerdict);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<PortfolioItem>(e =>
        {
            e.ToTable("portfolio");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol).IsUnique();
        });

        modelBuilder.Entity<DividendEntity>(e =>
        {
            e.ToTable("dividends");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Symbol, x.ExDate }).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<RefreshTokenEntity>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<DataProviderEntity>(e =>
        {
            e.ToTable("data_providers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.IsEnabled).HasDefaultValue(false);
            e.Property(x => x.HealthStatus).HasDefaultValue("unknown");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<NewsArticleEntity>(e => {
            e.ToTable("news_articles");
            e.HasIndex(x => x.Symbol);
            e.HasIndex(x => x.Sector);
            e.HasIndex(x => x.PublishedAt).IsDescending();
        });

        modelBuilder.Entity<SectorSentimentEntity>(e => {
            e.ToTable("sector_sentiment");
            e.HasIndex(x => new { x.Sector, x.CalculatedAt });
        });

        modelBuilder.Entity<CompanyFundamentalsEntity>(e =>
        {
            e.ToTable("company_fundamentals");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol).IsUnique();
            e.Property(x => x.FetchedAt).HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<InsiderTransactionEntity>(e =>
        {
            e.ToTable("insider_transactions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol);
            e.HasIndex(x => x.TransactionDate);
            e.HasIndex(x => new { x.Symbol, x.Name, x.TransactionDate }).IsUnique();
            e.Property(x => x.FetchedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<AlgoSettingsEntity>(e =>
        {
            e.ToTable("algo_settings");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Category, x.Key }).IsUnique();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<ClaudeApiLogEntity>(e =>
        {
            e.ToTable("claude_api_logs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        });

        // MarketSymbols
        modelBuilder.Entity<MarketSymbolEntity>(e =>
        {
            e.ToTable("market_symbols");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Symbol, x.Exchange }).IsUnique();
            e.HasIndex(x => x.Exchange);
            e.HasIndex(x => x.Country);
            e.Property(x => x.Symbol).HasMaxLength(50);
            e.Property(x => x.Exchange).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Sector).HasMaxLength(100);
            e.Property(x => x.Industry).HasMaxLength(100);
            e.Property(x => x.Country).HasMaxLength(5);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.SymbolType).HasMaxLength(50);
            e.Property(x => x.Logo).HasMaxLength(500);
        });

        // TrackedExchanges
        modelBuilder.Entity<TrackedExchangeEntity>(e =>
        {
            e.ToTable("tracked_exchanges");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExchangeCode).IsUnique();
            e.Property(x => x.ExchangeCode).HasMaxLength(20);
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Country).HasMaxLength(5);
        });
    }
}
