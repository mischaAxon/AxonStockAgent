using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SwingEdgeScreener;

var builder = Host.CreateApplicationBuilder(args);

// Config laden uit appsettings.json
builder.Services.Configure<ScreenerConfig>(
    builder.Configuration.GetSection("Screener"));

// HTTP clients
builder.Services.AddHttpClient<FinnhubClient>();
builder.Services.AddHttpClient<NotificationService>();

// Services registreren
builder.Services.AddSingleton<FinnhubClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = sp.GetRequiredService<IOptions<ScreenerConfig>>().Value;
    var logger = sp.GetRequiredService<ILogger<FinnhubClient>>();
    return new FinnhubClient(http, config.FinnhubApiKey, logger);
});

builder.Services.AddSingleton<NotificationService>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var config = sp.GetRequiredService<IOptions<ScreenerConfig>>().Value;
    var logger = sp.GetRequiredService<ILogger<NotificationService>>();
    return new NotificationService(http, config.TelegramBotToken, config.TelegramChatId, logger);
});

builder.Services.AddHostedService<ScreenerWorker>();

var host = builder.Build();
host.Run();
