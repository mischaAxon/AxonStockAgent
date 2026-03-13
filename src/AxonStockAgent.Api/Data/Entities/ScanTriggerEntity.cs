namespace AxonStockAgent.Api.Data.Entities;

public class ScanTriggerEntity
{
    public int Id { get; set; }

    /// <summary>"pending" | "running" | "completed" | "failed"</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Wie de scan heeft gestart ("admin", "api", etc.)</summary>
    public string RequestedBy { get; set; } = "admin";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Aantal verwerkte symbolen na afronding</summary>
    public int? ProcessedCount { get; set; }

    /// <summary>Aantal gegenereerde signalen na afronding</summary>
    public int? SignalsCount { get; set; }

    /// <summary>Foutmelding als de scan mislukt is</summary>
    public string? ErrorMessage { get; set; }
}
