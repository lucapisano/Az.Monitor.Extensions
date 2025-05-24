using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Az.Monitor.Extensions
{
    public class FileShareSpaceMonitoring
    {
        private readonly ILogger<FileShareSpaceMonitoring> _logger;
        private readonly IServiceProvider _sp;
        private readonly IConfiguration _config;
        private readonly IOptions<MonitoringFunctionOptions> _opt;

        public FileShareSpaceMonitoring(ILogger<FileShareSpaceMonitoring> logger, IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
            _config = sp.GetRequiredService<IConfiguration>();
            _opt = sp.GetRequiredService<IOptions<MonitoringFunctionOptions>>();
        }
        [Function("FileShareSpaceMonitoringHttp")]
        public async Task<HttpResponseData> RunHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                _logger?.LogTrace($"trace test");
                _logger?.LogDebug($"debug test");
                _logger?.LogInformation($"information test");
                _logger?.LogWarning($"warning test");
                await Task.Delay(1000);
                //var storageAccountId = await req.ReadAsStringAsync();
                //await RunAsync(storageAccountId);
            }
            catch (Exception ex)
            {
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync(ex.ToString());
            }
            return response;
        }
        [Function("FileShareSpaceMonitoringTimer")]
        public async Task RunTimer([TimerTrigger("%FileShareSpaceMonitoringCron%", RunOnStartup = false, UseMonitor = true)] TimerInfo myTimer)
        {
            _logger.LogInformation($"FileShareSpaceMonitoringTimer Started at: {DateTime.Now}. Last run time: {myTimer.ScheduleStatus?.Last}");
            await RunAsync();
            _logger?.LogInformation($"FileShareSpaceMonitoringTimer Next load time: {myTimer.ScheduleStatus?.Next}");
        }
        async Task RunAsync(string storageAccountId = null)
        {
            var telemetry = _sp.GetService<TelemetryClient>();
            if (telemetry == default)
                _logger?.LogWarning($"telemetry client does not have an InstrumentationKey. Data will not be sent to Application Insights");
            var credential = _opt.Value.AuthenticationOptions.GetDefaultAzureCredential(_config.GetValue<string>("AZURE_TENANT_ID"));
            ArmClient client = new ArmClient(credential);
            var subscriptions = client.GetSubscriptions();
            _logger?.LogInformation($"found {subscriptions.Count()} subscriptions");
            foreach (var subscription in subscriptions)
            {
                var accounts = subscription.GetStorageAccounts();
                _logger?.LogInformation($"iterating subscription {subscription.Data.DisplayName}. Found {accounts.Count()} storage accounts");
                foreach (var account in accounts)
                {
                    try
                    {
                        if (storageAccountId.HasAValue() && storageAccountId.Length > 3 && !storageAccountId.Equals(account.Id, StringComparison.InvariantCultureIgnoreCase))
                            continue;
                        if (account.Data.Kind != StorageKind.Storage && account.Data.Kind != StorageKind.StorageV2 && account.Data.Kind != StorageKind.FileStorage)
                        {
                            _logger?.LogInformation($"account {account.Id} ignored due to Kind {account.Data.Kind}");
                            continue;
                        }
                        var svc = account.GetFileService();
                        var shares = svc.GetFileShares();
                        _logger?.LogInformation($"found {shares.Count()} shares in account {account.Data.Name}");
                        foreach (var shareObj in shares)
                        {
                            var share = svc.GetFileShare(shareObj.Data.Name, expand: "stats").Value;
                            var shareId = share.Id;
                            try
                            {
                                long usageInMB = (share.Data.ShareUsageBytes ?? 0) / (long)1000000;
                                long quotaInMB = (long)(share.Data.ShareQuota ?? 1) * 1000;
                                double percent = Convert.ToDouble((double)usageInMB / (double)quotaInMB) * 100;
                                _logger?.LogInformation($"share {shareId} has {percent}% of space used. Space used: {share.Data.ShareUsageBytes} bytes");
                                if (telemetry != default)
                                {
                                    telemetry.TrackMetric("shareSpacePercentage", percent, new Dictionary<string, string> {
                                        { "shareId", shareId },
                                        { "usageMB", usageInMB.ToString() },
                                        { "quotaMB", quotaInMB.ToString() },
                                        { "storageAccountName", account.Data.Name },
                                        { "subscriptionName", subscription.Data.DisplayName }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, $"unable to retrieve share usage. Share: {shareId}");
                            }
                        }
                        if (telemetry != default)
                            telemetry.Flush();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"unable to retrieve storage account shares. Account: {account.Id}");
                    }
                }
            }
        }
    }
}
