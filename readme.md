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
