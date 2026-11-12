using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.GraphMailSender.Tests;

public class SenderTests
{
    private static EmailMessage Message() => new(null, new[] { "Recipient <to@example.com>" }, null, new[] { "bcc@example.com" }, null, "Subject", "Body", false, null);
    private static MailSenderOptions Options() => new()
    {
        From = "sender@example.com",
        Host = "smtp.office365.com",
        Port = 587,
        Username = "sender@example.com",
        SecureSocketOptions = Umbraco.Cms.Core.Configuration.Models.SecureSocketOptions.StartTls,
        Graph = new EntraOptions { TenantId = "11111111-1111-1111-1111-111111111111", ClientId = "22222222-2222-2222-2222-222222222222", ClientSecret = "test-secret", Mailbox = "sender@example.com" }
    };
    private sealed class Credential : TokenCredential
    {
        internal string[]? Scopes;
        public override AccessToken GetToken(TokenRequestContext context, CancellationToken cancellationToken)
        { Scopes = context.Scopes; return new AccessToken("access-token", DateTimeOffset.UtcNow.AddHours(1)); }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken cancellationToken) => new(GetToken(context, cancellationToken));
    }

    private sealed class Handler : HttpMessageHandler, IHttpClientFactory
    {
        internal HttpStatusCode Status = HttpStatusCode.Accepted;
        internal Uri? Uri; internal string? Authorization; internal string? ContentType; internal byte[]? Mime;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri; Authorization = request.Headers.Authorization?.ToString();
            if (request.Method == HttpMethod.Get) return new HttpResponseMessage(Status);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Mime = Convert.FromBase64String(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Status) { Content = new StringContent("private response details") };
        }
    }
    [Fact]
    public async Task GraphSendsMimeWithBccAndApplicationToken()
    {
        using var handler = new Handler(); var credential = new Credential();
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), credential, handler);
#if UMBRACO_19
        Assert.True(sender.IsEmailConfigured());
        Assert.Null(credential.Scopes);
#else
        Assert.True(sender.CanSendRequiredEmail());
#endif
        await sender.SendAsync(Message(), "test");
        Assert.Equal("https://graph.microsoft.com/v1.0/users/sender%40example.com/sendMail", handler.Uri!.AbsoluteUri);
        Assert.Equal("Bearer access-token", handler.Authorization);
        Assert.Equal("text/plain", handler.ContentType);
        Assert.Equal(new[] { "https://graph.microsoft.com/.default" }, credential.Scopes);
        Assert.NotNull(handler.Mime);
        using var mime = MimeMessage.Load(new MemoryStream(handler.Mime));
        Assert.Equal("Body", mime.TextBody?.TrimEnd('\r', '\n')); Assert.Equal("bcc@example.com", mime.Bcc.Mailboxes.Single().Address);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task GraphFailurePropagatesWithoutResponseDetails(int status)
    {
        using var handler = new Handler { Status = (HttpStatusCode)status };
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync(Message(), "test"));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private response", error.Message);
    }
    [Fact]
    public async Task OversizedMessageFailsBeforeHttpRequest()
    {
        using var handler = new Handler();
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), handler);
        var message = new EmailMessage(null, "to@example.com", "Subject", new string('x', 4 * 1024 * 1024), false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(message, "test"));
        Assert.Null(handler.Uri);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandledNotificationSuppressesDelivery(bool handled)
    {
        var events = new Mock<IEventAggregator>(); NotificationEmailModel? notification = null; string? emailType = null;
        events.Setup(x => x.PublishAsync(It.IsAny<SendEmailNotification>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailNotification, CancellationToken>((n, c) => { notification = n.Message; emailType = n.EmailType; if (handled) n.HandleEmail(); })
            .Returns(Task.CompletedTask);
        using var handler = new Handler();
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), events.Object, new Credential(), handler);
        await sender.SendAsync(Message(), "password-reset", true);
        Assert.NotNull(notification); Assert.Equal("sender@example.com", notification.From!.Address);
        Assert.Equal("password-reset", emailType); Assert.Equal("Body", notification.Body);
        Assert.Equal(handled, handler.Uri is null);
    }
#if UMBRACO_18 || UMBRACO_19
    [Fact]
    public async Task ConfiguredExpirationPreventsDelivery()
    {
        using var handler = new Handler();
        var options = Options(); options.EmailExpiration = TimeSpan.Zero;
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(options), Mock.Of<IEventAggregator>(), new Credential(), handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendAsync(Message(), "test"));
        Assert.Null(handler.Uri);
    }
#endif
#if UMBRACO_19
    [Fact]
    public async Task AvailabilityAcquiresGraphTokenWithoutSendingMail()
    {
        using var handler = new Handler(); var credential = new Credential();
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), credential, handler);
        Assert.True(await sender.IsEmailAvailableAsync(CancellationToken.None));
        Assert.Equal(new[] { "https://graph.microsoft.com/.default" }, credential.Scopes);
        Assert.Equal("https://graph.microsoft.com/v1.0/$metadata", handler.Uri!.AbsoluteUri);
        Assert.Equal("Bearer access-token", handler.Authorization);
        Assert.Null(handler.Mime);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task AvailabilityReturnsFalseWhenGraphIsUnavailable(int status)
    {
        using var handler = new Handler { Status = (HttpStatusCode)status };
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), handler);
        Assert.False(await sender.IsEmailAvailableAsync(CancellationToken.None));
        Assert.Null(handler.Mime);
    }

    [Fact]
    public async Task AvailabilityReturnsFalseWhenAuthenticationFails()
    {
        using var handler = new Handler();
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.Identity.AuthenticationFailedException("private provider details"));
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), credential.Object, handler);
        Assert.False(await sender.IsEmailAvailableAsync(CancellationToken.None));
        Assert.Null(handler.Uri);
    }

    [Fact]
    public async Task AvailabilityHonorsCancellation()
    {
        using var handler = new Handler();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var credential = new Mock<TokenCredential>();
        credential.Setup(x => x.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns((TokenRequestContext _, CancellationToken token) => ValueTask.FromCanceled<AccessToken>(token));
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), credential.Object, handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.IsEmailAvailableAsync(cancellation.Token));
        Assert.Null(handler.Uri);
    }
#endif
#if !UMBRACO_13
    [Fact]
    public async Task ExpiredMessageDoesNotReachTransport()
    {
        using var handler = new Handler();
        var sender = new EmailSender(Microsoft.Extensions.Options.Options.Create(Options()), Mock.Of<IEventAggregator>(), new Credential(), handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendAsync(Message(), "test", false, TimeSpan.Zero));
        Assert.Null(handler.Uri);
    }
#endif
}
