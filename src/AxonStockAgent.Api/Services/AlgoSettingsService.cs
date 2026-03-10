using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;

namespace AxonStockAgent.Api.Services;

public record WeightsConfig(double Technical = 0.35, double Ml = 0.25, double Sentiment = 0.15, double Claude = 0.25);
public record ThresholdsConfig(double Bull = 0.35, double Bear = -0.35);
public record TechnicalWeightsConfig(int Trend = 3, int Momentum = 2, int Volatility = 1, int Volume = 2);
public record ScanConfig(int IntervalMinutes = 15, int CooldownMinutes = 60, int CandleHistory = 100, string Timeframe = "D");
public record FeatureFlagsConfig(bool EnableMl = true, bool EnableClaude = true, bool EnableSentiment = true, bool EnableNewsFetcher = true);

public class AlgoSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AlgoSettingsService> _logger;

    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["weights"] = """{"technical":0.35,"ml":0.25,"sentiment":0.15,"claude":0.25}""",
        ["thresholds"] = """{"bull":0.35,"bear":-0.35}""",
        ["technical_weights"] = """{"trend":3,"momentum":2,"volatility":1,"volume":2}""",
        ["scan"] = """{"intervalMinutes":15,"cooldownMinutes":60,"candleHistory":100,"timeframe":"D"}""",
        ["features"] = """{"enableMl":true,"enableClaude":true,"enableSentiment":true,"enableNewsFetcher":true}"""
    };

    public AlgoSettingsService(AppDbContext db, ILogger<AlgoSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Dictionary<string, JsonElement>> GetAll()
    {
        var settings = await _db.AlgoSettings.ToListAsync();
        var result = new Dictionary<string, JsonElement>();
        foreach (var s in settings)
        {
            try { result[s.Key] = JsonDocument.Parse(s.Value).RootElement; }
            catch { /* skip malformed */ }
        }
        return result;
    }

    public async Task<JsonElement?> Get(string key)
    {
        var setting = await _db.AlgoSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return null;
        try { return JsonDocument.Parse(setting.Value).RootElement; }
        catch { return null; }
    }

    public async Task Set(string key, JsonElement value)
    {
        var json = value.GetRawText();
        var existing = await _db.AlgoSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing != null)
        {
            existing.Value = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.AlgoSettings.Add(new AlgoSettingsEntity { Key = key, Value = json, UpdatedAt = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated algo setting: {Key}", key);
    }

    public async Task ResetAll()
    {
        foreach (var (key, value) in Defaults)
        {
            var existing = await _db.AlgoSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.AlgoSettings.Add(new AlgoSettingsEntity { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
            }
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("Reset all algo settings to defaults");
    }

    public async Task<WeightsConfig> GetWeights()
    {
        var el = await Get("weights");
        if (el == null) return new WeightsConfig();
        return JsonSerializer.Deserialize<WeightsConfig>(el.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new WeightsConfig();
    }

    public async Task<ThresholdsConfig> GetThresholds()
    {
        var el = await Get("thresholds");
        if (el == null) return new ThresholdsConfig();
        return JsonSerializer.Deserialize<ThresholdsConfig>(el.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ThresholdsConfig();
    }

    public async Task<TechnicalWeightsConfig> GetTechnicalWeights()
    {
        var el = await Get("technical_weights");
        if (el == null) return new TechnicalWeightsConfig();
        return JsonSerializer.Deserialize<TechnicalWeightsConfig>(el.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TechnicalWeightsConfig();
    }

    public async Task<ScanConfig> GetScanConfig()
    {
        var el = await Get("scan");
        if (el == null) return new ScanConfig();
        return JsonSerializer.Deserialize<ScanConfig>(el.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ScanConfig();
    }

    public async Task<FeatureFlagsConfig> GetFeatureFlags()
    {
        var el = await Get("features");
        if (el == null) return new FeatureFlagsConfig();
        return JsonSerializer.Deserialize<FeatureFlagsConfig>(el.Value.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new FeatureFlagsConfig();
    }
}
