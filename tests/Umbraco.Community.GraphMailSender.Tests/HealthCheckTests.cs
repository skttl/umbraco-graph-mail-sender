using Azure;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Umbraco.Cms.Core.HealthChecks;
namespace Umbraco.Community.GraphMailSender.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task DisabledConfigurationReportsInfo()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var check = new MailSenderHealthCheck(new ConfigurationBuilder().Build(), provider);
        Assert.Equal(StatusResultType.Info, Assert.Single(await check.GetStatusAsync()).ResultType);
    }
    [Fact]
    public async Task ValidConfigurationAndTokenReportSuccess()
    {
        var values = ConfigurationTests.Settings(); var services = ConfigurationTests.Register(values);
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("private-token", DateTimeOffset.UtcNow.AddHours(1)));
        services.AddKeyedSingleton<TokenCredential>(MailSenderRegistration.ServiceKey, credential.Object);
        using var provider = services.BuildServiceProvider();
        var check = new MailSenderHealthCheck(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), provider);
        var result = Assert.Single(await check.GetStatusAsync());
        Assert.Equal(StatusResultType.Success, result.ResultType);
        Assert.Contains("not verified", result.Message);
        Assert.DoesNotContain("private-token", result.Message);
    }
    [Fact]
    public async Task AuthenticationFailureHidesSensitiveDetails()
    {
        var values = ConfigurationTests.Settings(); var services = ConfigurationTests.Register(values);
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("private-token test-secret"));
        services.AddKeyedSingleton<TokenCredential>(MailSenderRegistration.ServiceKey, credential.Object);
        using var provider = services.BuildServiceProvider();
        var result = Assert.Single(await new MailSenderHealthCheck(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), provider).GetStatusAsync());
        Assert.Equal(StatusResultType.Error, result.ResultType);
        Assert.DoesNotContain("private-token", result.Message); Assert.DoesNotContain("test-secret", result.Message);
    }
    [Fact]
    public async Task InvalidConfigurationReportsError()
    {
        var values = ConfigurationTests.Settings(); values.Remove("Umbraco:CMS:Global:Smtp:Graph:ClientSecret");
        using var provider = ConfigurationTests.Register(values).BuildServiceProvider();
        var check = new MailSenderHealthCheck(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), provider);
        var result = Assert.Single(await check.GetStatusAsync());
        Assert.Equal(StatusResultType.Error, result.ResultType); Assert.Contains("ClientSecret", result.Message);
    }
}
