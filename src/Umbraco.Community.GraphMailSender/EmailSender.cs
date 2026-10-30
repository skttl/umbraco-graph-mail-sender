using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;
using Umbraco.Cms.Core.Notifications;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MimeKit;
namespace Umbraco.Community.GraphMailSender;

public sealed class EmailSender : IEmailSender
{
    private readonly MailSenderOptions _options;
    private readonly IEventAggregator _events;
    private readonly TokenCredential _credential;
    private readonly IHttpClientFactory _clients;
    // shortcut: MIME payload is limited to 3 MiB before outer base64; add upload sessions if larger mail is required.
    private const int MaxMimeBytes = 3 * 1024 * 1024;

    public EmailSender(IOptions<MailSenderOptions> options, IEventAggregator events,
        [FromKeyedServices(MailSenderRegistration.ServiceKey)] TokenCredential credential, IHttpClientFactory clients)
    {
        _options = options.Value;
        _events = events;
        _credential = credential;
        _clients = clients;
    }

    public bool CanSendRequiredEmail() => true;

#if !UMBRACO_18
    public Task SendAsync(EmailMessage message, string emailType) => SendInternalAsync(message, emailType, false, null);
    public Task SendAsync(EmailMessage message, string emailType, bool enableNotification) => SendInternalAsync(message, emailType, enableNotification, null);
#endif
#if !UMBRACO_13
    public Task SendAsync(EmailMessage message, string emailType, bool enableNotification = false, TimeSpan? expires = null)
        => SendInternalAsync(message, emailType, enableNotification, expires);
#endif

    private async Task SendInternalAsync(EmailMessage message, string emailType, bool enableNotification, TimeSpan? expires)
    {
#if UMBRACO_18
        expires ??= _options.EmailExpiration;
#endif
        if (expires <= TimeSpan.Zero) throw new OperationCanceledException("The email has expired.");
        using var timeout = new CancellationTokenSource();
        if (expires is not null) timeout.CancelAfter(expires.Value);
        var cancellationToken = timeout.Token;
        if (enableNotification)
        {
            var notification = new SendEmailNotification(MessageConversion.ToNotification(message, _options.From), emailType);
            await _events.PublishAsync(notification, cancellationToken);
            if (notification.IsHandled) return;
        }
        cancellationToken.ThrowIfCancellationRequested();

        using var mime = await MessageConversion.ToMimeAsync(message, _options.From, cancellationToken, MaxMimeBytes);
        using var stream = new MemoryStream();
        var format = FormatOptions.Default.Clone();
        format.NewLineFormat = NewLineFormat.Dos;
        format.HiddenHeaders.Remove(HeaderId.Bcc);
        await mime.WriteToAsync(format, stream, cancellationToken);
        if (stream.Length > MaxMimeBytes) throw new InvalidOperationException("Graph email exceeds the supported 3 MiB MIME size.");
        var token = await _credential.GetTokenAsync(new TokenRequestContext(["https://graph.microsoft.com/.default"]), cancellationToken);
        var mailbox = Uri.EscapeDataString(_options.Graph.Mailbox);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{mailbox}/sendMail");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = new StringContent(Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length), Encoding.ASCII, "text/plain");
        using var client = _clients.CreateClient(MailSenderRegistration.ServiceKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Accepted)
            throw new HttpRequestException($"Microsoft Graph sendMail returned HTTP {(int)response.StatusCode}. No automatic retry was attempted.", null, response.StatusCode);

    }
}
