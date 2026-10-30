using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.HealthChecks;
namespace Umbraco.Community.GraphMailSender;

[HealthCheck("1AFA0A82-6405-4A8E-8B1B-B73893777429", "Graph mail sender",
    Description = "Validates configuration and Entra authentication without sending email.", Group = "Configuration")]
public sealed class MailSenderHealthCheck(IConfiguration configuration, IServiceProvider services) : HealthCheck
{
#if UMBRACO_13
    public override Task<IEnumerable<HealthCheckStatus>> GetStatus() => GetStatusAsync();
    public async Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
#else
    public override async Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
#endif
    {
        if (!configuration.GetSection("Umbraco:CMS:Global:Smtp:Graph").Exists())
            return [Status("Smtp:Graph is not configured. This mail sender is inactive.", StatusResultType.Info)];
        try
        {
            _ = services.GetRequiredService<IOptions<MailSenderOptions>>().Value;
            var credential = services.GetRequiredKeyedService<TokenCredential>(MailSenderRegistration.ServiceKey);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await credential.GetTokenAsync(new TokenRequestContext(["https://graph.microsoft.com/.default"]), timeout.Token);
            return [Status("Configuration is valid and Entra issued an access token. Mailbox send permission and delivery are not verified. No test email was sent.", StatusResultType.Success)];
        }
        catch (OptionsValidationException error)
        {
            return [Status("Configuration is invalid: " + string.Join("; ", error.Failures), StatusResultType.Error)];
        }
        catch (Exception)
        {
            // Provider exception messages can contain credentials, paths or token response details.
            return [Status("Authentication check failed. Check configuration, certificate availability, credential expiry and access to Microsoft Entra.", StatusResultType.Error)];
        }
    }

    public override HealthCheckStatus ExecuteAction(HealthCheckAction action) => throw new NotSupportedException("This health check has no actions.");
    private static HealthCheckStatus Status(string message, StatusResultType type) => new(message) { ResultType = type };
}
