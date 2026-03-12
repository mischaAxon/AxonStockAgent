using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Services;
using AxonStockAgent.Core.Models;
using AxonStockAgent.Worker;
using AxonStockAgent.Worker.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Database (zelfde connection string als API)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// HTTP client factory (voor providers)
builder.Services.AddHttpClient();

// In-memory cache voor candle data
builder.Services.AddMemoryCache(options =>
{
    // Limiet: ~500 symbolen × ~90 candles = ~45.000 entries
    options.SizeLimit = 50_000;
});
builder.Services.AddSingleton<CandleCacheService>();

// Services hergebruiken van Api project
builder.Services.AddScoped<ProviderManager>();
builder.Services.AddScoped<AlgoSettingsService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<FundamentalsService>();
builder.Services.AddScoped<ClaudeApiKeyProvider>();

// Legacy config (voor Telegram, Claude API key, etc.)
builder.Services.Configure<ScreenerConfig>(
    builder.Configuration.GetSection("Screener"));

// Worker
builder.Services.AddHostedService<ScreenerWorker>();

var host = builder.Build();

// Ensure Claude provider exists
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.DataProviders.AnyAsync(p => p.Name == "claude"))
    {
        db.DataProviders.Add(new DataProviderEntity
        {
            Name               = "claude",
            DisplayName        = "Claude AI (Anthropic)",
            ProviderType       = "ai",
            IsEnabled          = false,
            RateLimitPerMinute = 50,
            SupportsEu         = true,
            SupportsUs         = true,
            IsFree             = false,
            MonthlyCost        = 0,
            HealthStatus       = "unknown",
        });
        await db.SaveChangesAsync();
    }
}

host.Run();
