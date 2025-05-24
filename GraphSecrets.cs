using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Net;

namespace Az.Monitor.Extensions
{
    public class GraphSecrets
    {
        private readonly ILogger<GraphSecrets> _logger;
        private readonly IServiceProvider _sp;
        private readonly IConfiguration _config;
        private readonly IOptions<MonitoringFunctionOptions> _opt;

        public GraphSecrets(ILogger<GraphSecrets> logger, IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
            _config = sp.GetRequiredService<IConfiguration>();
            _opt = sp.GetRequiredService<IOptions<MonitoringFunctionOptions>>();
        }
        [Function("GraphSecretsHttp")]
        public async Task<HttpResponseData> RunHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                var appId = await req.ReadAsStringAsync();
                await RunAsync(appId);
            }
            catch (Exception ex)
            {
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync(ex.ToString());
            }
            return response;
        }
        [Function("GraphSecretsTimer")]
        public async Task RunTimer([TimerTrigger("%GraphSecretsCron%", RunOnStartup = false, UseMonitor = true)] TimerInfo myTimer)
        {
            _logger.LogInformation($"GraphSecrets Started at: {DateTime.Now}. Last run time: {myTimer.ScheduleStatus?.Last}");
            await RunAsync();
            _logger?.LogInformation($"GraphSecrets Next load time: {myTimer.ScheduleStatus?.Next}");
        }
        async Task<List<TOut>> GetFromGraphAsync<TOut, TResponse>(GraphServiceClient graphServiceClient, TResponse? collectionResponse)
            where TResponse : Microsoft.Kiota.Abstractions.Serialization.IParsable, Microsoft.Kiota.Abstractions.Serialization.IAdditionalDataHolder, new()
        {
            List<TOut> appList = new();

            PageIterator<TOut, TResponse> pageIterator =
                PageIterator<TOut, TResponse>.CreatePageIterator(
                    graphServiceClient, collectionResponse,
                    app => { appList.Add(app); return true; });

            await pageIterator.IterateAsync();

            return appList;
        }
        async Task<List<Application>> GetApplicationsAsync(GraphServiceClient graphServiceClient)
            => await GetFromGraphAsync<Application, ApplicationCollectionResponse>(graphServiceClient, await graphServiceClient.Applications.GetAsync(req => req.QueryParameters.Top = 999));
        /*
        async Task<List<Application>> GetApplicationsAsync(GraphServiceClient graphServiceClient)
        {
            ApplicationCollectionResponse? appCollectionResponse =
                await graphServiceClient.Applications.GetAsync();

            List<Application> appList = new();

            PageIterator<Application, ApplicationCollectionResponse> pageIterator =
                PageIterator<Application, ApplicationCollectionResponse>.CreatePageIterator(
                    graphServiceClient, appCollectionResponse,
                    app => { appList.Add(app); return true; });

            await pageIterator.IterateAsync();

            return appList;
        }
        */
        /// <summary>
        /// è richiesto il permesso Application.Read.All
        /// seguire questa guida per assegnarlo alla managed identity: https://techcommunity.microsoft.com/t5/azure-integration-services-blog/grant-graph-api-permission-to-managed-identity-object/ba-p/2792127
        /// https://github.com/Azure/azure-docs-powershell-azuread/blob/main/azureadps-2.0/AzureAD/New-AzureADServiceAppRoleAssignment.md
        /// </summary>
        /// <param name="appId"></param>
        /// <returns></returns>
        async Task RunAsync(string appId = null)
        {
            var telemetry = _sp.GetService<TelemetryClient>();
            if (telemetry == default)
                _logger?.LogWarning($"telemetry client does not have an InstrumentationKey. Data will not be sent to Application Insights");
            var credential = _opt.Value.AuthenticationOptions.GetDefaultAzureCredential(_config.GetValue<string>("AZURE_TENANT_ID"));
            string[] scopes = new[] { "https://graph.microsoft.com/.default" };
            var graphServiceClient = new GraphServiceClient(credential, scopes);
            IEnumerable<Application> apps = new List<Application>();
            _logger?.LogInformation($"retrieving apps from tenant");
            if (appId.HasAValue())
                apps = new List<Application> { await graphServiceClient.ApplicationsWithAppId(appId).GetAsync() };
            else
                apps = await GetApplicationsAsync(graphServiceClient);
            _logger?.LogInformation($"found {apps.Count()} apps");
            foreach (var app in apps)
            {
                try
                {
                    _logger?.LogInformation($"retrieving data for appId: {app.AppId}. {app.PasswordCredentials?.Count} secrets, {app.KeyCredentials?.Count} certificates");
                    foreach (var secret in (app.PasswordCredentials ?? new List<PasswordCredential>()))
                    {
                        try
                        {
                            _logger?.LogInformation($"appId: {app.AppId}. Secret {secret.KeyId} expires on {secret.EndDateTime}, displayName {secret.DisplayName}");
                            if (!secret.EndDateTime.HasValue)
                                continue;
                            var days = (secret.EndDateTime - DateTime.UtcNow).Value.TotalDays;
                            if (telemetry != default)
                            {
                                telemetry.TrackMetric("secretsDaysExpiry", days, new Dictionary<string, string> {
                                    { "applicationId", app.AppId },
                                    { "applicationName", app.DisplayName },
                                    { "secretId", secret.KeyId.ToString() },
                                    { "expiryDate", secret.EndDateTime.GetValueOrDefault().ToString("yyyy-MM-dd") }
                                });
                                telemetry.TrackTrace("secretsDaysExpiry", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information, new Dictionary<string, string> {
                                    { "applicationId", app.AppId },
                                    { "applicationName", app.DisplayName },
                                    { "secretId", secret.KeyId.ToString() },
                                    { "days", days.ToString() },
                                    { "expiryDate", secret.EndDateTime.GetValueOrDefault().ToString("yyyy-MM-dd") }
                                });
                                telemetry.TrackEvent("secretsDaysExpiry", new Dictionary<string, string> {
                                    { "applicationId", app.AppId },
                                    { "applicationName", app.DisplayName },
                                    { "secretId", secret.KeyId.ToString() },
                                    { "days", days.ToString() },
                                    { "expiryDate", secret.EndDateTime.GetValueOrDefault().ToString("yyyy-MM-dd") }
                                });                                
                            }
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, $"error iterating secret from appId {appId}, secretId {secret.KeyId}");
                        }
                    }
                    foreach (var certificate in (app.KeyCredentials ?? new List<KeyCredential>()))
                    {
                        try
                        {
                            _logger?.LogInformation($"certificate {certificate.KeyId} expires on {certificate.EndDateTime}, displayName {certificate.DisplayName}");
                            if (!certificate.EndDateTime.HasValue)
                                continue;
                            var days = (certificate.EndDateTime - DateTime.UtcNow).Value.TotalDays;
                            if (telemetry != default)
                            {
                                telemetry.TrackMetric("certificateDaysExpiry", days, new Dictionary<string, string> {
                                    { "applicationId", app.AppId },
                                    { "applicationName", app.DisplayName },
                                    { "certificateId", certificate.KeyId.ToString() },
                                    { "expiryDate", certificate.EndDateTime.GetValueOrDefault().ToString("yyyy-MM-dd") }
                                });
                                telemetry.TrackTrace("certificateDaysExpiry", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information, new Dictionary<string, string> {
                                    { "applicationId", app.AppId },
                                    { "applicationName", app.DisplayName },
                                    { "certificateId", certificate.KeyId.ToString() },
                                    { "days", days.ToString() },
                                    { "expiryDate", certificate.EndDateTime.GetValueOrDefault().ToString("yyyy-MM-dd") }
                                });
                                telemetry.TrackEvent("certificateDaysExpiry", new Dictionary<string, string> {
                                    { "applicationId", app.AppId },
                                    { "applicationName", app.DisplayName },
                                    { "certificateId", certificate.KeyId.ToString() },
                                    { "days", days.ToString() },
                                    { "expiryDate", certificate.EndDateTime.GetValueOrDefault().ToString("yyyy-MM-dd") }
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, $"error iterating certificate from appId {appId}, certificateId {certificate.KeyId}");
                        }
                    }
                    if (telemetry != default)
                    {
                        telemetry.Flush();
                        //_logger?.LogInformation($"waiting for telemetry client to flush");
                        await Task.Delay(200);
                    }
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, $"error iterating appId {appId}");
                }
            }
        }
    }
}
