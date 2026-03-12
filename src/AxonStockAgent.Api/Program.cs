using System.Text;
using AxonStockAgent.Api.BackgroundServices;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// JWT Authentication
var jwtSecret   = builder.Configuration["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret is not configured");
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "AxonStockAgent";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AxonStockAgent";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer          = true,
            ValidIssuer             = jwtIssuer,
            ValidateAudience        = true,
            ValidAudience           = jwtAudience,
            ValidateLifetime        = true,
            ClockSkew               = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:80")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// HTTP client factory (gebruikt door ProviderManager)
builder.Services.AddHttpClient();

// In-memory cache (voor QuoteCacheService)
builder.Services.AddMemoryCache(options => { options.SizeLimit = 10_000; });

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AxonStockAgent API", Version = "v1" });
});

// Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProviderManager>();
builder.Services.AddScoped<SectorService>();
builder.Services.AddScoped<NewsService>();
builder.Services.AddScoped<FundamentalsService>();
builder.Services.AddScoped<AlgoSettingsService>();
builder.Services.AddHostedService<NewsFetcherService>();
builder.Services.AddScoped<SignalOutcomeService>();
builder.Services.AddHostedService<OutcomeTrackerService>();
builder.Services.AddScoped<QuoteCacheService>();
builder.Services.AddScoped<ExchangeImportService>();
builder.Services.AddScoped<IndexImportService>();
builder.Services.AddScoped<ClaudeIndexService>();
builder.Services.AddScoped<ClaudeApiKeyProvider>();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Ensure Claude provider exists
using (var scope = app.Services.CreateScope())
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

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
