namespace AxonStockAgent.Api.Data.Entities;

public class DataProviderEntity
{
    public int Id { get; set; }

    /// <summary>Unieke sleutel, bijv. "finnhub", "eodhd"</summary>
    public string Name { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>"market_data" | "news" | "fundamentals" | "all"</summary>
    public string ProviderType { get; set; } = "market_data";

    public bool IsEnabled { get; set; } = false;

    public string? ApiKeyEncrypted { get; set; }

    public string? ConfigJson { get; set; }

    public int RateLimitPerMinute { get; set; } = 60;

    public bool SupportsEu { get; set; } = false;

    public bool SupportsUs { get; set; } = true;

    public bool IsFree { get; set; } = false;

    public decimal MonthlyCost { get; set; } = 0;

    /// <summary>"unknown" | "healthy" | "degraded" | "down"</summary>
    public string HealthStatus { get; set; } = "unknown";

    public DateTime? LastHealthCheck { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
