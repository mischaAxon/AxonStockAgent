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
    }
}
