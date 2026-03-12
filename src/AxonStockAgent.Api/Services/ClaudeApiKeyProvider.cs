using AxonStockAgent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Leest de Claude API key uit de data_providers tabel.
/// Fallback naar IConfiguration voor backward compatibility.
/// </summary>
public class ClaudeApiKeyProvider
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ClaudeApiKeyProvider(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Haalt de Claude API key op. Prioriteit:
    /// 1. data_providers tabel (naam = "claude")
    /// 2. IConfiguration["Claude:ApiKey"]
    /// 3. IConfiguration["ANTHROPIC_API_KEY"]
    /// 4. Environment variable ANTHROPIC_API_KEY
    /// </summary>
    public async Task<string?> GetApiKeyAsync()
    {
        // 1. Database provider
        var provider = await _db.DataProviders
            .FirstOrDefaultAsync(p => p.Name == "claude" && p.IsEnabled);
        if (provider != null && !string.IsNullOrEmpty(provider.ApiKeyEncrypted))
            return provider.ApiKeyEncrypted; // TODO: decrypt wanneer encryptie is geïmplementeerd

        // 2-4. Fallback naar config/env
        return _config["Claude:ApiKey"]
            ?? _config["ANTHROPIC_API_KEY"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    /// <summary>
    /// Check of Claude beschikbaar en geconfigureerd is.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        var key = await GetApiKeyAsync();
        return !string.IsNullOrEmpty(key);
    }
}
