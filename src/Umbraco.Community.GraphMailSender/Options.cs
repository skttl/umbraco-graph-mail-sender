using System.Net.Mail;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
namespace Umbraco.Community.GraphMailSender;

public sealed class EntraOptions
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    public string Mailbox { get; set; } = "";
}

public sealed class MailSenderOptions : SmtpSettings
{
    public EntraOptions Graph { get; set; } = new();
}

internal sealed class MailOptionsValidator : IValidateOptions<MailSenderOptions>
{
    public ValidateOptionsResult Validate(string? name, MailSenderOptions options)
    {
        var errors = new List<string>();
        var auth = options.Graph;
        if (auth is null) return ValidateOptionsResult.Fail("Smtp:Graph must be configured.");
        if (!Guid.TryParse(auth.TenantId, out _)) errors.Add("Graph:TenantId must be a tenant GUID.");
        if (!Guid.TryParse(auth.ClientId, out _)) errors.Add("Graph:ClientId must be an application GUID.");
        if (string.IsNullOrWhiteSpace(auth.ClientSecret) == string.IsNullOrWhiteSpace(auth.CertificatePath))
            errors.Add("Configure exactly one of Graph:ClientSecret and Graph:CertificatePath.");
        if (!string.IsNullOrWhiteSpace(auth.CertificatePath) && !Path.IsPathFullyQualified(auth.CertificatePath))
            errors.Add("Graph:CertificatePath must be an absolute path to a PFX certificate.");
        if (!MessageConversion.IsMailbox(options.From)) errors.Add("Smtp:From must be a valid email address.");
        if (options.DeliveryMethod != SmtpDeliveryMethod.Network || !string.IsNullOrWhiteSpace(options.PickupDirectoryLocation))
            errors.Add("GraphMailSender requires Network delivery and no PickupDirectoryLocation.");
        if (!MessageConversion.IsBareMailbox(auth.Mailbox) && !Guid.TryParse(auth.Mailbox, out _))
            errors.Add("Graph:Mailbox must be a mailbox email address or user object GUID.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
