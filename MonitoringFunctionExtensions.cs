using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Az.Monitor.Extensions
{
    public static class MonitoringFunctionExtensions
    {
        public static TokenCredential GetDefaultAzureCredential(this DefaultAzureCredentialOptions? options, string? debugTenantId = default)
        {
            TokenCredential credential = default;
            if (Debugger.IsAttached)
            {
                var opt = new VisualStudioCredentialOptions
                {
                    TenantId = debugTenantId,
                };
                credential = new VisualStudioCredential(opt);
            }
            else
                credential = new DefaultAzureCredential(options);
            return credential;
        }
        public static IServiceCollection AddMonitoringFunction(this IServiceCollection sc, IConfiguration config)
        {
            return sc
                .ConfigureOption<MonitoringFunctionOptions>(config)
                ;
        }
        /*
        public static T Clone<T>(this T source)
        {
            var serialized = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<T>(serialized);
        }
        */
        public static string Truncate(this string s, int length, ILogger logger = null)
        {
            string result;

            if (string.IsNullOrEmpty(s) || s.Length <= length)
            {
                result = s;
            }
            else
            {
                if (length <= 0)
                {
                    result = string.Empty;
                }
                else
                {
                    result = s.Substring(0, length);

                    logger?.LogWarning("Truncated string:{stringToTruncate}", s);
                }
            }

            return result;
        }
        public static bool ContainsNullable(this IEnumerable<string> input, string item, StringComparison stringComparison = StringComparison.InvariantCultureIgnoreCase) => input != null && input.Any(x => x.Equals(item, stringComparison));
        public static bool ContainsNullable<T>(this IEnumerable<T> input, T item) => input != null && input.Contains(item);
        public static bool AnyNullable<T>(this IEnumerable<T> input) => input != null && input.Any();
        public static bool HasNoValue(this string? input) => string.IsNullOrWhiteSpace(input);
        public static bool HasAValue(this string? input) => !string.IsNullOrWhiteSpace(input);
        public static bool IsGuid(this string? input)
        {
            if (!input.HasAValue())
                return false;
            try
            {
                var g = new Guid(input);
                return true;
            }
            catch
            {
                return input.Count(x => x == '-') >= 3;
            }
        }
        public static IServiceCollection ConfigureOption<ConfigurationType>(this IServiceCollection services, IConfiguration config) where ConfigurationType : class, new()
        {
            services.Configure<ConfigurationType>(config.GetSection(typeof(ConfigurationType).Name));
            return services;
        }
        public static DateTime SetTime(this DateTime dt, int h, int m, int s)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, h, m, s);
        }
    }
}
