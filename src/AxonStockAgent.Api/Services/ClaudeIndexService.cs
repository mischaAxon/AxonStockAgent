using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AxonStockAgent.Api.Services;

/// <summary>
/// Vraagt Claude AI om de huidige samenstelling van een beursindex te retourneren.
/// Gebruikt voor indexen waar geen gratis API beschikbaar is (bijv. AEX, AMX, AScX).
/// </summary>
public class ClaudeIndexService
{
    private readonly HttpClient _http;
    private readonly ClaudeApiKeyProvider _keyProvider;
    private readonly ILogger<ClaudeIndexService> _logger;

    private const string Model = "claude-sonnet-4-20250514";

    public ClaudeIndexService(HttpClient http, ClaudeApiKeyProvider keyProvider, ILogger<ClaudeIndexService> logger)
    {
        _http = http;
        _keyProvider = keyProvider;
        _logger = logger;
    }

    /// <summary>
    /// Vraag Claude om de componenten van een index.
    /// Retourneert een lijst van (Code, Name, Sector) tuples.
    /// </summary>
    public async Task<IndexComponentResult[]> GetIndexComponentsViaAI(string indexName, string exchangeCode)
    {
        var apiKey = await _keyProvider.GetApiKeyAsync() ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Claude API key niet geconfigureerd");
            return Array.Empty<IndexComponentResult>();
        }

        var prompt = $$$"""
            Je bent een financieel data-expert. Geef de HUIDIGE samenstelling van de {{{indexName}}} index.

            Retourneer UITSLUITEND een JSON array (geen markdown, geen toelichting). Elk element bevat:
            - "code": het ticker-symbool zoals het op de beurs wordt verhandeld (bijv. "ASML" voor Euronext Amsterdam, "AAPL" voor US)
            - "name": de volledige bedrijfsnaam
            - "sector": de GICS-sector (bijv. "Technology", "Healthcare", "Financials")

            De exchange code is "{{{exchangeCode}}}".

            Voorbeeld formaat:
            [{"code":"ASML","name":"ASML Holding","sector":"Technology"},{"code":"INGA","name":"ING Group","sector":"Financials"}]

            Geef ALLE componenten van de index, niet een subset. Zorg dat de data zo actueel mogelijk is.
            """;

        try
        {
            var request = new
            {
                model = Model,
                max_tokens = 4000,
                messages = new[] { new { role = "user", content = prompt } }
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Claude API error {Status}: {Error}", response.StatusCode, error);
                return Array.Empty<IndexComponentResult>();
            }

            var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
            var text = result?.Content?.FirstOrDefault()?.Text ?? "";

            // Parse JSON array uit de response
            var json = text.Trim();
            if (json.StartsWith("```")) json = json.Split('\n', 2).Last();
            if (json.EndsWith("```")) json = json[..json.LastIndexOf("```")];
            json = json.Trim();

            var components = JsonSerializer.Deserialize<IndexComponentResult[]>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Claude retourneerde {Count} componenten voor {Index}", components?.Length ?? 0, indexName);
            return components ?? Array.Empty<IndexComponentResult>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Claude index import mislukt voor {Index}: {Message}", indexName, ex.Message);
            return Array.Empty<IndexComponentResult>();
        }
    }

    public record IndexComponentResult
    {
        [JsonPropertyName("code")] public string Code { get; init; } = "";
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("sector")] public string Sector { get; init; } = "";
    }

    private record ClaudeResponse(
        [property: JsonPropertyName("content")] ClaudeContentBlock[]? Content
    );
    private record ClaudeContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text
    );
}
