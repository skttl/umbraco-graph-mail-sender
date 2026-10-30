using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Mail;

namespace Umbraco.Community.GraphMailSender;

public sealed class MailSenderComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddGraphMailSender(builder.Config);
}

public static class MailSenderRegistration
{
    internal const string ServiceKey = "Umbraco.Community.GraphMailSender";
    public static IServiceCollection AddGraphMailSender(this IServiceCollection services, IConfiguration configuration)
    {
        var smtp = configuration.GetSection("Umbraco:CMS:Global:Smtp");
        if (!smtp.GetSection("Graph").Exists()) return services;
        if (smtp.GetSection("OAuth").Exists())
            throw new InvalidOperationException("Configure either Smtp:Graph or Smtp:OAuth, not both.");
        services.AddOptions<MailSenderOptions>().Bind(smtp).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MailSenderOptions>, MailOptionsValidator>());
        services.AddKeyedSingleton<X509Certificate2>(ServiceKey, (provider, _) =>
        {
            var auth = provider.GetRequiredService<IOptions<MailSenderOptions>>().Value.Graph;
#if NET10_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12FromFile(auth.CertificatePath!, auth.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
#else
            return new X509Certificate2(auth.CertificatePath!, auth.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
#endif
        });
        services.AddKeyedSingleton<TokenCredential>(ServiceKey, (provider, _) =>
        {
            var auth = provider.GetRequiredService<IOptions<MailSenderOptions>>().Value.Graph;
            return !string.IsNullOrWhiteSpace(auth.ClientSecret)
                ? new ClientSecretCredential(auth.TenantId, auth.ClientId, auth.ClientSecret)
                : new ClientCertificateCredential(auth.TenantId, auth.ClientId, provider.GetRequiredKeyedService<X509Certificate2>(ServiceKey));
        });
        services.AddHttpClient(ServiceKey).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.Replace(ServiceDescriptor.Singleton<IEmailSender, EmailSender>());
        return services;
    }
}
