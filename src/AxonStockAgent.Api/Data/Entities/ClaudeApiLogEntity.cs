using System.ComponentModel.DataAnnotations;

namespace AxonStockAgent.Api.Data.Entities;

public class ClaudeApiLogEntity
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Symbol { get; set; } = "";

    /// <summary>
    /// success, api_error, parse_error, empty_response, timeout, unknown
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "";

    /// <summary>
    /// HTTP status code van de Claude API response (null bij timeout/netwerk-error)
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Bij success: het geparsede direction (BUY/SELL/HOLD). Bij error: null.
    /// </summary>
    [MaxLength(10)]
    public string? Direction { get; set; }

    /// <summary>
    /// Bij success: de confidence score. Bij error: null.
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// Bij error: de foutmelding (max 500 chars). Bij success: null.
    /// </summary>
    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Bij parse_error: de eerste 500 chars van het ruwe Claude-antwoord.
    /// Handig om te debuggen waarom het parsen mislukte.
    /// </summary>
    [MaxLength(500)]
    public string? RawResponseSnippet { get; set; }

    /// <summary>
    /// Duur van de API-call in milliseconden.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Het Claude model dat gebruikt is.
    /// </summary>
    [MaxLength(50)]
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
