using System.Text.Json;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public class AlgoSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AlgoSettingsService> _logger;

    // Static om herhaalde allocatie te voorkomen
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AlgoSettingsService(AppDbContext db, ILogger<AlgoSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Alle settings ophalen, gegroepeerd per category</summary>
    public async Task<Dictionary<string, List<AlgoSettingDto>>> GetAllGroupedAsync()
    {
        var settings = await _db.AlgoSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync();

        return settings
            .GroupBy(s => s.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new AlgoSettingDto(
                    s.Id, s.Category, s.Key, s.Value,
                    s.Description, s.ValueType,
                    s.MinValue, s.MaxValue, s.UpdatedAt
                )).ToList()
            );
    }

    /// <summary>Eén setting updaten met validatie</summary>
    public async Task<AlgoSettingDto> UpdateSettingAsync(int id, string newValue)
    {
        var setting = await _db.AlgoSettings.FindAsync(id)
            ?? throw new KeyNotFoundException($"Setting met id {id} niet gevonden");

        // Type validatie
        ValidateValue(setting, newValue);

        var oldValue = setting.Value;
        setting.Value = newValue;
        setting.UpdatedAt = DateTime.UtcNow;

        // Als het een weight is: valideer dat de groep nog optelt tot 1.0
        if (setting.Category == "weights")
        {
            await ValidateWeightsSum(setting.Key, newValue);
        }

        // Threshold cross-validatie: buy moet > sell
        if (setting.Category == "thresholds")
        {
            await ValidateThresholds(setting.Key, newValue);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Algo setting updated: {Category}.{Key} = {OldValue} → {NewValue}",
            setting.Category, setting.Key, oldValue, newValue);

        return new AlgoSettingDto(
            setting.Id, setting.Category, setting.Key, setting.Value,
            setting.Description, setting.ValueType,
            setting.MinValue, setting.MaxValue, setting.UpdatedAt
        );
    }

    /// <summary>Alle settings resetten naar seed-defaults</summary>
    public async Task ResetToDefaultsAsync()
    {
        var current = await _db.AlgoSettings.ToListAsync();
        _db.AlgoSettings.RemoveRange(current);
        await _db.SaveChangesAsync();

        var seedSql = @"
            INSERT INTO algo_settings (category, key, value, description, value_type, min_value, max_value) VALUES
                ('weights', 'technical_weight',  '0.30', 'Gewicht technische analyse in eindscore',    'decimal', 0.0, 1.0),
                ('weights', 'ml_weight',         '0.25', 'Gewicht ML-voorspelling in eindscore',       'decimal', 0.0, 1.0),
                ('weights', 'sentiment_weight',  '0.20', 'Gewicht nieuwssentiment in eindscore',       'decimal', 0.0, 1.0),
                ('weights', 'claude_weight',     '0.15', 'Gewicht Claude AI-analyse in eindscore',     'decimal', 0.0, 1.0),
                ('weights', 'fundamental_weight','0.10', 'Gewicht fundamentele analyse in eindscore',  'decimal', 0.0, 1.0),
                ('thresholds', 'buy_threshold',     '0.65', 'Minimum score voor BUY signaal',       'decimal', 0.0, 1.0),
                ('thresholds', 'sell_threshold',    '0.35', 'Maximum score voor SELL signaal',       'decimal', 0.0, 1.0),
                ('thresholds', 'squeeze_threshold', '0.80', 'Minimum score voor SQUEEZE signaal',   'decimal', 0.0, 1.0),
                ('scan', 'realtime_mode',             'false', 'Realtime scanmodus — scant elke N minuten tijdens markturen (standaard: EOD dagelijks 22:30 UTC)', 'boolean', null, null),
                ('scan', 'realtime_interval_minutes', '30', 'Interval in minuten bij realtime scan (alleen actief als realtime_mode aan staat)', 'integer', 5, 360),
                ('scan', 'lookback_days',             '90',    'Aantal dagen historische data',          'integer', 30,  365),
                ('scan', 'min_volume',                '100000','Minimum gemiddeld volume',               'integer', 0,   null),
                ('notifications', 'notify_buy',     'true',  'Notificeer bij BUY signalen',     'boolean', null, null),
                ('notifications', 'notify_sell',    'true',  'Notificeer bij SELL signalen',    'boolean', null, null),
                ('notifications', 'notify_squeeze', 'true',  'Notificeer bij SQUEEZE signalen', 'boolean', null, null)
            ON CONFLICT (category, key) DO NOTHING;";

        await _db.Database.ExecuteSqlRawAsync(seedSql);
        _logger.LogWarning("Algo settings reset to defaults");
    }

    /// <summary>Haal een specifieke waarde op als decimal (voor ScreenerWorker)</summary>
    public async Task<decimal> GetDecimalAsync(string category, string key, decimal defaultValue = 0m)
    {
        var setting = await _db.AlgoSettings
            .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);

        if (setting == null) return defaultValue;
        return decimal.TryParse(setting.Value, out var val) ? val : defaultValue;
    }

    /// <summary>Haal een specifieke waarde op als bool (voor ScreenerWorker)</summary>
    public async Task<bool> GetBoolAsync(string category, string key, bool defaultValue = false)
    {
        var setting = await _db.AlgoSettings
            .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);

        if (setting == null) return defaultValue;
        return bool.TryParse(setting.Value, out var val) ? val : defaultValue;
    }

    // ── Private validatie ──────────────────────────────────────────────────────

    private static void ValidateValue(AlgoSettingsEntity setting, string newValue)
    {
        switch (setting.ValueType)
        {
            case "decimal":
                if (!double.TryParse(newValue, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var dVal))
                    throw new ArgumentException($"'{newValue}' is geen geldig decimaal getal");
                if (setting.MinValue.HasValue && dVal < setting.MinValue.Value)
                    throw new ArgumentException($"Waarde {dVal} is kleiner dan minimum {setting.MinValue}");
                if (setting.MaxValue.HasValue && dVal > setting.MaxValue.Value)
                    throw new ArgumentException($"Waarde {dVal} is groter dan maximum {setting.MaxValue}");
                break;

            case "integer":
                if (!int.TryParse(newValue, out var iVal))
                    throw new ArgumentException($"'{newValue}' is geen geldig geheel getal");
                if (setting.MinValue.HasValue && iVal < setting.MinValue.Value)
                    throw new ArgumentException($"Waarde {iVal} is kleiner dan minimum {setting.MinValue}");
                if (setting.MaxValue.HasValue && iVal > setting.MaxValue.Value)
                    throw new ArgumentException($"Waarde {iVal} is groter dan maximum {setting.MaxValue}");
                break;

            case "boolean":
                if (newValue != "true" && newValue != "false")
                    throw new ArgumentException($"Boolean waarde moet 'true' of 'false' zijn, niet '{newValue}'");
                break;
        }
    }

    private async Task ValidateWeightsSum(string changingKey, string newValue)
    {
        var weights = await _db.AlgoSettings
            .Where(s => s.Category == "weights")
            .ToListAsync();

        double sum = 0;
        foreach (var w in weights)
        {
            if (w.Key == changingKey)
                sum += double.Parse(newValue, System.Globalization.CultureInfo.InvariantCulture);
            else
                sum += double.Parse(w.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (Math.Abs(sum - 1.0) > 0.001)
        {
            throw new ArgumentException(
                $"Gewichten moeten optellen tot 1.0 (huidige som zou {sum:F3} worden). " +
                $"Pas eerst andere gewichten aan.");
        }
    }

    private async Task ValidateThresholds(string changingKey, string newValue)
    {
        var thresholds = await _db.AlgoSettings
            .Where(s => s.Category == "thresholds")
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        thresholds[changingKey] = newValue;

        var buy = double.Parse(thresholds.GetValueOrDefault("buy_threshold", "0.65"),
            System.Globalization.CultureInfo.InvariantCulture);
        var sell = double.Parse(thresholds.GetValueOrDefault("sell_threshold", "0.35"),
            System.Globalization.CultureInfo.InvariantCulture);
        var squeeze = double.Parse(thresholds.GetValueOrDefault("squeeze_threshold", "0.80"),
            System.Globalization.CultureInfo.InvariantCulture);

        if (sell >= buy)
            throw new ArgumentException(
                $"Sell threshold ({sell:F2}) moet lager zijn dan buy threshold ({buy:F2})");

        if (squeeze <= buy)
            throw new ArgumentException(
                $"Squeeze threshold ({squeeze:F2}) moet hoger zijn dan buy threshold ({buy:F2})");
    }
}

public record AlgoSettingDto(
    int Id,
    string Category,
    string Key,
    string Value,
    string? Description,
    string ValueType,
    double? MinValue,
    double? MaxValue,
    DateTime UpdatedAt
);
