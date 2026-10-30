using MimeKit;
using Umbraco.Cms.Core.Models.Email;
namespace Umbraco.Community.GraphMailSender.Tests;

public class MessageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MimePreservesContentRecipientsAndAttachments(bool html)
    {
        using var attachment = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var source = new EmailMessage(null, new[] { "Recipient <to@example.com>" }, new[] { "cc@example.com" }, new[] { "bcc@example.com" }, new[] { "reply@example.com" }, "Subject", "Body", html, new[] { new EmailMessageAttachment(attachment, "data.pdf") });
        using (var mime = await MessageConversion.ToMimeAsync(source, "sender@example.com"))
        {
            Assert.Equal("sender@example.com", mime.From.Mailboxes.Single().Address);
            Assert.Equal("Recipient", mime.To.Mailboxes.Single().Name);
            Assert.Equal("cc@example.com", mime.Cc.Mailboxes.Single().Address);
            Assert.Equal("bcc@example.com", mime.Bcc.Mailboxes.Single().Address);
            Assert.Equal("reply@example.com", mime.ReplyTo.Mailboxes.Single().Address);
            Assert.Equal("Subject", mime.Subject);
            Assert.Equal("Body", html ? mime.HtmlBody : mime.TextBody);
            var part = Assert.IsType<MimePart>(Assert.Single(mime.Attachments));
            Assert.Equal("application/pdf", part.ContentType.MimeType);
            using var bytes = new MemoryStream(); Assert.NotNull(part.Content); part.Content.DecodeTo(bytes);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes.ToArray());
        }
        Assert.True(attachment.CanRead);
    }
    [Fact]
    public async Task ExplicitFromWinsOverDefault()
    {
        using var mime = await MessageConversion.ToMimeAsync(new EmailMessage("Name <explicit@example.com>", "to@example.com", "Subject", "Body", false), "fallback@example.com");
        Assert.Equal("explicit@example.com", mime.From.Mailboxes.Single().Address);
    }
    [Fact]
    public async Task InvalidRecipientFailsBeforeDelivery()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => MessageConversion.ToMimeAsync(new EmailMessage(null, "invalid", "Subject", "Body", false), "sender@example.com"));
    }

    [Fact]
    public async Task AttachmentReadLimitRejectsOversizedInputAndLeavesStreamOpen()
    {
        using var stream = new MemoryStream(new byte[8]);
        var message = new EmailMessage(null, new[] { "to@example.com" }, null, null, null, "Subject", "Body", false, new[] { new EmailMessageAttachment(stream, "file.bin") });
        await Assert.ThrowsAsync<InvalidOperationException>(() => MessageConversion.ToMimeAsync(message, "sender@example.com", maxAttachmentBytes: 4));
        Assert.True(stream.CanRead);
    }
    [Theory]
    [InlineData("user@example.com\r\nBcc: other@example.com")]
    [InlineData("invalid")]
    public void InvalidAddressDoesNotPassValidation(string address) => Assert.False(MessageConversion.IsMailbox(address));

    [Fact]
    public async Task CancellationStopsMessageConversion()
    {
        var source = new EmailMessage(null, "to@example.com", "Subject", "Body", false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MessageConversion.ToMimeAsync(source, "sender@example.com", new CancellationToken(true)));
    }

    private sealed class SlowAttachment : MemoryStream
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
    [Fact]
    public async Task CancellationInterruptsAnActiveAttachmentRead()
    {
        using var stream = new SlowAttachment(); using var cancellation = new CancellationTokenSource();
        var source = new EmailMessage(null, new[] { "to@example.com" }, null, null, null, "Subject", "Body", false, new[] { new EmailMessageAttachment(stream, "slow.bin") });
        var conversion = MessageConversion.ToMimeAsync(source, "sender@example.com", cancellation.Token);
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(stream.CanRead);
    }
}
