using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Worker.Services;

/// <summary>
/// Roept Claude API aan voor AI-verrijking van signalen.
/// Logt elke interactie (succes én falen) naar de claude_api_logs tabel.
/// </summary>
public class ClaudeAnalysisService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<ClaudeAnalysisService> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    private const string Model = "claude-sonnet-4-20250514";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClaudeAnalysisService(
        HttpClient http,
        string apiKey,
        ILogger<ClaudeAnalysisService> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _http = http;
        _apiKey = apiKey;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Vraag Claude om een assessment op basis van technische analyse, sentiment en nieuws.
    /// Retourneert null als Claude niet bereikbaar is (graceful degradation).
    /// </summary>
    public async Task<ClaudeAssessment?> AnalyzeAsync(
        string symbol,
        IndicatorResult indicators,
        double sentimentScore,
        string[] recentHeadlines,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogDebug("Claude API key niet geconfigureerd, skip analyse voor {Symbol}", symbol);
            return null;
        }

        var sw = Stopwatch.StartNew();
        string? rawText = null;
        int? httpStatus = null;

        try
        {
            var prompt = BuildPrompt(symbol, indicators, sentimentScore, recentHeadlines);

            var request = new
            {
                model = Model,
                max_tokens = 500,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(httpRequest, ct);
            httpStatus = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();
                _logger.LogWarning("Claude API error {Status} voor {Symbol}: {Error}",
                    response.StatusCode, symbol, errorBody);

                await LogInteractionAsync(symbol, "api_error", httpStatus, null, null,
                    $"{response.StatusCode}: {Truncate(errorBody, 400)}", null, (int)sw.ElapsedMilliseconds);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ClaudeApiResponse>(JsonOptions, ct);
            rawText = result?.Content?.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(rawText))
            {
                sw.Stop();
                _logger.LogWarning("Leeg antwoord van Claude voor {Symbol}", symbol);

                await LogInteractionAsync(symbol, "empty_response", httpStatus, null, null,
                    "Response content was empty or null", null, (int)sw.ElapsedMilliseconds);
                return null;
            }

            var assessment = ParseAssessment(rawText, symbol);
            sw.Stop();

            if (assessment == null)
            {
                await LogInteractionAsync(symbol, "parse_error", httpStatus, null, null,
                    "JSON parse failed", Truncate(rawText, 500), (int)sw.ElapsedMilliseconds);
                return null;
            }

            // Success!
            await LogInteractionAsync(symbol, "success", httpStatus, assessment.Direction,
                assessment.Confidence, null, null, (int)sw.ElapsedMilliseconds);

            return assessment;
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            await LogInteractionAsync(symbol, "timeout", httpStatus, null, null,
                "Request was cancelled or timed out", null, (int)sw.ElapsedMilliseconds);
            return null;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Claude analyse mislukt voor {Symbol}", symbol);

            await LogInteractionAsync(symbol, "unknown", httpStatus, null, null,
                Truncate(ex.Message, 500), Truncate(rawText, 500), (int)sw.ElapsedMilliseconds);
            return null;
        }
    }

    /// <summary>
    /// Log een Claude API interactie naar de database.
    /// Fire-and-forget met eigen scope — mag de scan niet vertragen.
    /// </summary>
    private async Task LogInteractionAsync(
        string symbol, string status, int? httpStatusCode,
        string? direction, double? confidence,
        string? errorMessage, string? rawResponseSnippet,
        int durationMs)
    {
        if (_scopeFactory == null) return; // graceful als geen DI beschikbaar

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.ClaudeApiLogs.Add(new ClaudeApiLogEntity
            {
                Symbol = symbol,
                Status = status,
                HttpStatusCode = httpStatusCode,
                Direction = direction,
                Confidence = confidence,
                ErrorMessage = errorMessage,
                RawResponseSnippet = rawResponseSnippet,
                DurationMs = durationMs,
                Model = Model,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Logging mag nooit de scan laten crashen
            _logger.LogDebug(ex, "Kon Claude API log niet opslaan voor {Symbol}", symbol);
        }
    }

    private static string BuildPrompt(
        string symbol, IndicatorResult indicators,
        double sentimentScore, string[] headlines)
    {
        var headlineText = headlines.Length > 0
            ? string.Join("\n", headlines.Take(5).Select(h => $"- {h}"))
            : "- Geen recent nieuws beschikbaar";

        return $$"""
            Je bent een ervaren financieel analist. Analyseer het volgende aandeel en geef een gestructureerde beoordeling.

            ## Aandeel: {{symbol}}

            ## Technische Indicatoren
            - Trend Score: {{indicators.TrendScore:F2}} ({{indicators.TrendDesc}})
            - Momentum Score: {{indicators.MomentumScore:F2}} ({{indicators.MomDesc}})
            - Volatiliteit Score: {{indicators.VolatilityScore:F2}} ({{indicators.VolDesc}})
            - Volume Score: {{indicators.VolumeScore:F2}} ({{indicators.VolumDesc}})
            - Genormaliseerde Score: {{indicators.NormScore:F2}}
            - Squeeze Gedetecteerd: {{indicators.SqueezeDetected}}

            ## Sentiment Score: {{sentimentScore:F2}} (-1 = zeer negatief, +1 = zeer positief)

            ## Recent Nieuws
            {{headlineText}}

            ## Instructies
            Antwoord UITSLUITEND met een JSON object (geen markdown, geen toelichting buiten JSON):
            {
              "confidence": <0.0-1.0>,
              "direction": "<BUY|SELL|HOLD>",
              "reasoning": "<1-2 zinnen>",
              "newsConfirms": <true|false>,
              "keyFactors": ["<factor1>", "<factor2>", "<factor3>"]
            }
            """;
    }

    private ClaudeAssessment? ParseAssessment(string text, string symbol)
    {
        try
        {
            var json = text.Trim();
            if (json.StartsWith("```")) json = json.Split('\n', 2).Last();
            if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];
            json = json.Trim();

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ClaudeAssessment(
                Confidence: root.GetProperty("confidence").GetDouble(),
                Direction: root.GetProperty("direction").GetString() ?? "HOLD",
                Reasoning: root.GetProperty("reasoning").GetString() ?? "",
                NewsConfirms: root.GetProperty("newsConfirms").GetBoolean(),
                KeyFactors: root.TryGetProperty("keyFactors", out var kf)
                    ? kf.EnumerateArray().Select(e => e.GetString() ?? "").ToArray()
                    : Array.Empty<string>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kon Claude response niet parsen voor {Symbol}: {Text}",
                symbol, text[..Math.Min(200, text.Length)]);
            return null;
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value == null ? null : value.Length <= maxLength ? value : value[..maxLength];

    // ── Response DTOs ──────────────────────────────────────────────────────────

    private record ClaudeApiResponse(
        [property: JsonPropertyName("content")] ClaudeContentBlock[]? Content
    );

    private record ClaudeContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text
    );
}
