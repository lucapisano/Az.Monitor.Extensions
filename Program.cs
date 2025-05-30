using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Configuration//.AddJsonFile("host.json", true)
            .AddJsonFile("appsettings.json", true)
            .AddUserSecrets<Program>(optional: true);
builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<LoggerFilterOptions>(options =>
{
    // The Application Insights SDK adds a default logging filter that instructs ILogger to capture only Warning and more severe logs. Application Insights requires an explicit override.
    // Log levels can also be configured using appsettings.json. For more information, see https://learn.microsoft.com/en-us/azure/azure-monitor/app/worker-service#ilogger-logs
    LoggerFilterRule toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName
        == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

    if (toRemove is not null)
    {
        options.Rules.Remove(toRemove);
    }
});
builder.Services
    .AddOptions()
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Build().Run();
