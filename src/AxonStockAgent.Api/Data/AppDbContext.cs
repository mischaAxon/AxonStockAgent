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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchlistItem>(e =>
        {
            e.ToTable("watchlist");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Symbol).IsUnique();
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
    }
}
