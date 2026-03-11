using System.Net.Http.Json;
using AxonStockAgent.Core.Models;

namespace AxonStockAgent.Worker.Services;

/// <summary>
/// Stuurt signaal-notificaties via Telegram Bot API.
/// Graceful degradation: als token niet geconfigureerd is, wordt niets verstuurd.
/// </summary>
public class TelegramNotificationService
{
    private readonly HttpClient _http;
    private readonly string _botToken;
    private readonly string _chatId;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(HttpClient http, string botToken, string chatId,
        ILogger<TelegramNotificationService> logger)
    {
        _http = http;
        _botToken = botToken;
        _chatId = chatId;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_botToken) && !string.IsNullOrEmpty(_chatId);

    public async Task SendSignalAsync(AiEnrichedSignal signal, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        var emoji = signal.FinalVerdict switch
        {
            "BUY" => "🟢",
            "SELL" => "🔴",
            "SQUEEZE" => "🟡",
            _ => "⚪"
        };

        var claudeInfo = signal.Claude != null
            ? $"\n🤖 Claude: {signal.Claude.Direction} ({signal.Claude.Confidence:P0})\n📝 {signal.Claude.Reasoning}"
            : "";

        var message = $"""
            {emoji} **{signal.FinalVerdict}** — {signal.BaseSignal.Symbol}

            💰 Prijs: €{signal.BaseSignal.Price:F2}
            📊 Eindscore: {signal.FinalScore:F2}
            📈 Tech: {signal.BaseSignal.Score:F2} | Sentiment: {signal.SentimentScore:F2}
            {claudeInfo}

            _{signal.Summary}_
            """;

        await SendMessageAsync(message, ct);
    }

    public async Task SendMessageAsync(string message, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        try
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
            var payload = new { chat_id = _chatId, text = message, parse_mode = "Markdown" };
            await _http.PostAsJsonAsync(url, payload, ct);
            _logger.LogDebug("Telegram bericht verzonden");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram bericht verzenden mislukt");
        }
    }
}
