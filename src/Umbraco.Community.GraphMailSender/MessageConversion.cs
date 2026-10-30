using MimeKit;
using Umbraco.Cms.Core.Models.Email;
namespace Umbraco.Community.GraphMailSender;

internal static class MessageConversion
{
    internal static bool IsMailbox(string? value) => !string.IsNullOrWhiteSpace(value)
        && !value.ContainsAnyNewline() && MailboxAddress.TryParse(value, out var mailbox) && mailbox.Address.Contains('@');

    private static bool ContainsAnyNewline(this string value) => value.Contains('\r') || value.Contains('\n');

    internal static bool IsBareMailbox(string? value) => IsMailbox(value) && MailboxAddress.Parse(value!).Address == value;

    private static MailboxAddress Parse(string? value)
    {
        if (!IsMailbox(value)) throw new ArgumentException("Email contains an invalid mailbox address.");
        return MailboxAddress.Parse(value!);
    }

    internal static async Task<MimeMessage> ToMimeAsync(EmailMessage source, string fallback, CancellationToken cancellationToken = default, long maxAttachmentBytes = long.MaxValue)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mime = new MimeMessage();
        try
        {
            mime.From.Add(Parse(string.IsNullOrEmpty(source.From) ? fallback : source.From));
            Add(mime.To, source.To);
            Add(mime.Cc, source.Cc);
            Add(mime.Bcc, source.Bcc);
            Add(mime.ReplyTo, source.ReplyTo);
            if (mime.To.Count == 0) throw new ArgumentException("Email needs at least one To recipient.");
            mime.Subject = source.Subject ?? "";
            var body = new BodyBuilder();
            if (source.IsBodyHtml) body.HtmlBody = source.Body;
            else body.TextBody = source.Body;
            long total = 0;
            foreach (var attachment in source.Attachments ?? [])
            {
                using var buffer = new MemoryStream();
                var bytes = new byte[81920];
                int count;
                while ((count = await attachment.Stream.ReadAsync(bytes.AsMemory(), cancellationToken)) > 0)
                {
                    total += count;
                    if (total > maxAttachmentBytes) throw new InvalidOperationException("Email exceeds the supported attachment size.");
                    buffer.Write(bytes, 0, count);
                }
                body.Attachments.Add(attachment.FileName, buffer.ToArray());
            }
            mime.Body = body.ToMessageBody();
            return mime;
        }
        catch { mime.Dispose(); throw; }
    }

    private static void Add(InternetAddressList target, IEnumerable<string?>? source)
    {
        foreach (var address in source ?? [])
            if (!string.IsNullOrWhiteSpace(address)) target.Add(Parse(address));
    }

    internal static NotificationEmailModel ToNotification(EmailMessage source, string fallback)
    {
        static NotificationEmailAddress? Convert(string? value) => IsMailbox(value)
            ? new NotificationEmailAddress(MailboxAddress.Parse(value!).Address, MailboxAddress.Parse(value!).Name ?? "") : null;
        static IEnumerable<NotificationEmailAddress> Addresses(IEnumerable<string?>? values) =>
            (values ?? []).Select(Convert).OfType<NotificationEmailAddress>().ToArray();
        return new NotificationEmailModel(Convert(string.IsNullOrEmpty(source.From) ? fallback : source.From),
            Addresses(source.To), Addresses(source.Cc), Addresses(source.Bcc), Addresses(source.ReplyTo),
            source.Subject, source.Body, source.Attachments, source.IsBodyHtml);
    }
}
