using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Mail;
namespace Umbraco.Community.GraphMailSender.Tests;

public class ConfigurationTests
{
    internal static Dictionary<string, string?> Settings() => new()
    {
        ["Umbraco:CMS:Global:Smtp:From"] = "sender@example.com",
        ["Umbraco:CMS:Global:Smtp:Host"] = "smtp.office365.com",
        ["Umbraco:CMS:Global:Smtp:Port"] = "587",
        ["Umbraco:CMS:Global:Smtp:Username"] = "sender@example.com",
        ["Umbraco:CMS:Global:Smtp:SecureSocketOptions"] = "StartTls",
        ["Umbraco:CMS:Global:Smtp:Graph:TenantId"] = "11111111-1111-1111-1111-111111111111",
        ["Umbraco:CMS:Global:Smtp:Graph:ClientId"] = "22222222-2222-2222-2222-222222222222",
        ["Umbraco:CMS:Global:Smtp:Graph:ClientSecret"] = "test-secret",
        ["Umbraco:CMS:Global:Smtp:Graph:Mailbox"] = "sender@example.com"
    };
    internal static ServiceCollection Register(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddGraphMailSender(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services;
    }
    [Fact]
    public void AbsentSectionKeepsExistingSender()
    {
        var services = new ServiceCollection();
        var existing = ServiceDescriptor.Singleton<IEmailSender>(new Moq.Mock<IEmailSender>().Object);
        ((ICollection<ServiceDescriptor>)services).Add(existing);
        services.AddGraphMailSender(new ConfigurationBuilder().Build());
        Assert.Same(existing, Assert.Single(services, x => x.ServiceType == typeof(IEmailSender)));
    }
    [Fact]
    public void ConfiguredSectionReplacesExistingSender()
    {
        var services = Register(Settings());
        Assert.Equal(typeof(EmailSender), Assert.Single(services, x => x.ServiceType == typeof(IEmailSender)).ImplementationType);
    }
    [Fact]
    public void BindsExistingSmtpSettings()
    {
        using var provider = Register(Settings()).BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailSenderOptions>>().Value;
        Assert.Equal("sender@example.com", options.From);
        Assert.Equal("test-secret", options.Graph.ClientSecret);
        Assert.Equal("sender@example.com", options.Graph.Mailbox);
    }
    [Theory]
    [InlineData("TenantId")]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    public void MissingAuthenticationFailsValidation(string key)
    {
        var config = Settings(); config.Remove("Umbraco:CMS:Global:Smtp:Graph:" + key);
        using var provider = Register(config).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MailSenderOptions>>().Value);
    }
    [Fact]
    public void AmbiguousCredentialsFailValidation()
    {
        var config = Settings(); config["Umbraco:CMS:Global:Smtp:Graph:CertificatePath"] = "unused.pfx";
        using var provider = Register(config).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MailSenderOptions>>().Value);
    }
    [Fact]
    public void ConflictingTransportsFailRegistration()
    {
        var config = Settings(); config["Umbraco:CMS:Global:Smtp:OAuth:TenantId"] = "other";
        Assert.Throws<InvalidOperationException>(() => Register(config));
    }
    [Theory]
    [InlineData("From", "invalid")]
    [InlineData("DeliveryMethod", "SpecifiedPickupDirectory")]
    [InlineData("Graph:Mailbox", "invalid")]
    [InlineData("Graph:Mailbox", "Name <sender@example.com>")]
    public void InvalidSettingsFailValidation(string key, string value)
    {
        var config = Settings(); config["Umbraco:CMS:Global:Smtp:" + key] = value;
        using var provider = Register(config).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MailSenderOptions>>().Value);
    }

    [Fact]
    public void StartupValidationRejectsMissingCredentials()
    {
        var values = Settings(); values.Remove("Umbraco:CMS:Global:Smtp:Graph:ClientSecret");
        using var provider = Register(values).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }
    [Fact]
    public void ComposerRegistersResolvableSenderWithoutOverridingOtherCredentials()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build();
        var services = new ServiceCollection();
        services.AddSingleton(Moq.Mock.Of<Umbraco.Cms.Core.Events.IEventAggregator>());
        var unrelatedCredential = Moq.Mock.Of<Azure.Core.TokenCredential>();
        services.AddSingleton(unrelatedCredential);
        var builder = new Moq.Mock<Umbraco.Cms.Core.DependencyInjection.IUmbracoBuilder>();
        builder.SetupGet(x => x.Services).Returns(services); builder.SetupGet(x => x.Config).Returns(config);
        new MailSenderComposer().Compose(builder.Object);
        using var provider = services.BuildServiceProvider();
        Assert.IsType<EmailSender>(provider.GetRequiredService<IEmailSender>());
        Assert.Same(unrelatedCredential, provider.GetRequiredService<Azure.Core.TokenCredential>());
    }
    [Fact]
    public void CertificateCredentialLoadsFromConfiguredPfx()
    {
        using var key = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=mail-test", key, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pfx");
        try
        {
            File.WriteAllBytes(file, certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "test-password"));
            var values = Settings(); values.Remove("Umbraco:CMS:Global:Smtp:Graph:ClientSecret");
            values["Umbraco:CMS:Global:Smtp:Graph:CertificatePath"] = file;
            values["Umbraco:CMS:Global:Smtp:Graph:CertificatePassword"] = "test-password";
            using var provider = Register(values).BuildServiceProvider();
            Assert.IsType<Azure.Identity.ClientCertificateCredential>(provider.GetRequiredKeyedService<Azure.Core.TokenCredential>(MailSenderRegistration.ServiceKey));
        }
        finally { File.Delete(file); }
    }
}
