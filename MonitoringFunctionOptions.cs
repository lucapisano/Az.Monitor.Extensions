using Azure.Identity;

namespace Az.Monitor.Extensions
{
    public class MonitoringFunctionOptions
    {
        public DefaultAzureCredentialOptions AuthenticationOptions { get; set; } = new DefaultAzureCredentialOptions();
    }
}
