using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Services;
using AxonStockAgent.Core.Models;
using AxonStockAgent.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Database (zelfde connection string als API)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// HTTP client factory (voor providers)
builder.Services.AddHttpClient();

// Services hergebruiken van Api project
builder.Services.AddScoped<ProviderManager>();
builder.Services.AddScoped<AlgoSettingsService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<FundamentalsService>();

// Legacy config (voor Telegram, Claude API key, etc.)
builder.Services.Configure<ScreenerConfig>(
    builder.Configuration.GetSection("Screener"));

// Worker
builder.Services.AddHostedService<ScreenerWorker>();

var host = builder.Build();
host.Run();
