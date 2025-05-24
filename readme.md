# Mini Documentation

This project includes two main functions that can be scheduled using NCRON expressions set as environment variables:

- `%FileShareSpaceMonitoringCron%` for the File Share Space Monitoring function
- `%GraphSecretsMonitoringCron%` for the Graph Secrets Monitoring function

## File Share Space Monitoring Function

**Purpose:**  
Monitors space percentage of a file share to prevents issues related to insufficient space.

**Scheduling:**  
The execution schedule is controlled by the `%FileShareSpaceMonitoringCron%` environment variable, which should be set to a valid NCRON expression.

**Example NCRON Expressions:**
- Every 6 hours: `0 */6 * * *`
- Every day at midnight: `0 0 * * *`

**Permissions required:**

Monitoring Reader on the scope covered by the function (eg. subscription or management group)
Permissions can be given to the function managed identity
Refer to [this page] (https://techcommunity.microsoft.com/t5/azure-integration-services-blog/grant-graph-api-permission-to-managed-identity-object/ba-p/2792127) for role assignment to Managed Identity

---

## Graph Secrets Monitoring Function

**Purpose:**  
Monitors secrets stored in a Microsoft Graph-integrated service (such as Azure Key Vault or Microsoft Entra ID) for changes, expirations, or compliance issues. This function helps ensure that secrets are rotated regularly and that expired or soon-to-expire secrets are detected and handled.

**Scheduling:**  
The execution schedule is controlled by the `%GraphSecretsMonitoringCron%` environment variable, which should be set to a valid NCRON expression.

**Example NCRON Expressions:**
- Every 6 hours: `0 */6 * * *`
- Every day at midnight: `0 0 * * *`

**Typical Use Cases:**
- Alerting administrators about secrets that are about to expire

**Permissions required:**

Application.Read.All on the Entra tenant
Permissions can be given to the function managed identity

---

**Note:**  
If you need to test the functions manually you can invoke the HTTP trigger of each function

## Deploy on Azure Function

1. Create an Azure Function with an Application Insights resource. Function runtime must be .NET 8 Isolated. Tier can be Consumption, Flex Consumption or Premium.
2. Setup deployment to point to the GitHub repository of this project: https://github.com/lucapisano/Az.Monitor.Extensions
3. Set the environment variables for the NCRON expressions:
   - `%FileShareSpaceMonitoringCron%`
   - `%GraphSecretsMonitoringCron%`