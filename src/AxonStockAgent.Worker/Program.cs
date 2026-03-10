using AxonStockAgent.Core.Models;
using AxonStockAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ScreenerConfig>(
    builder.Configuration.GetSection("Screener"));

builder.Services.AddHttpClient();
builder.Services.AddHostedService<ScreenerWorker>();

var host = builder.Build();
host.Run();
