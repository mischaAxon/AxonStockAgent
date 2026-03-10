using System;
using System.Collections.Generic;
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

    // Fix 2: één gedeelde instantie in plaats van per-aanroep aanmaken
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    // Fix 1: validatie voor weights en thresholds
    public async Task Set(string key, JsonElement value)
    {
        var json = value.GetRawText();

        if (key == "weights")
        {
            var weights = JsonSerializer.Deserialize<WeightsConfig>(json, _jsonOptions);
            if (weights != null)
            {
                var sum = weights.Technical + weights.Ml + weights.Sentiment + weights.Claude;
                if (Math.Abs(sum - 1.0) > 0.01)
                    throw new ArgumentException($"Weights moeten optellen tot 1.0, huidige som: {sum:F2}");
                if (weights.Technical < 0 || weights.Ml < 0 || weights.Sentiment < 0 || weights.Claude < 0)
                    throw new ArgumentException("Weights mogen niet negatief zijn");
            }
        }

        if (key == "thresholds")
        {
            var thresholds = JsonSerializer.Deserialize<ThresholdsConfig>(json, _jsonOptions);
            if (thresholds != null)
            {
                if (thresholds.Bull <= 0 || thresholds.Bull > 1)
                    throw new ArgumentException("Bull drempel moet tussen 0 en 1 liggen");
                if (thresholds.Bear >= 0 || thresholds.Bear < -1)
                    throw new ArgumentException("Bear drempel moet tussen -1 en 0 liggen");
            }
        }

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

    // Fix 2: gebruik _jsonOptions in alle typed getters
    public async Task<WeightsConfig> GetWeights()
    {
        var el = await Get("weights");
        if (el == null) return new WeightsConfig();
        return JsonSerializer.Deserialize<WeightsConfig>(el.Value.GetRawText(), _jsonOptions) ?? new WeightsConfig();
    }

    public async Task<ThresholdsConfig> GetThresholds()
    {
        var el = await Get("thresholds");
        if (el == null) return new ThresholdsConfig();
        return JsonSerializer.Deserialize<ThresholdsConfig>(el.Value.GetRawText(), _jsonOptions) ?? new ThresholdsConfig();
    }

    public async Task<TechnicalWeightsConfig> GetTechnicalWeights()
    {
        var el = await Get("technical_weights");
        if (el == null) return new TechnicalWeightsConfig();
        return JsonSerializer.Deserialize<TechnicalWeightsConfig>(el.Value.GetRawText(), _jsonOptions) ?? new TechnicalWeightsConfig();
    }

    public async Task<ScanConfig> GetScanConfig()
    {
        var el = await Get("scan");
        if (el == null) return new ScanConfig();
        return JsonSerializer.Deserialize<ScanConfig>(el.Value.GetRawText(), _jsonOptions) ?? new ScanConfig();
    }

    public async Task<FeatureFlagsConfig> GetFeatureFlags()
    {
        var el = await Get("features");
        if (el == null) return new FeatureFlagsConfig();
        return JsonSerializer.Deserialize<FeatureFlagsConfig>(el.Value.GetRawText(), _jsonOptions) ?? new FeatureFlagsConfig();
    }
}
