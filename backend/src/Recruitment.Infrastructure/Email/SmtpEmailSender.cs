using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Notifications;

namespace Recruitment.Infrastructure.Email;

/// <summary>
/// MailKit ile SMTP gönderimi. Takvim daveti hem "text/calendar" alternatif gövde (Outlook ve Gmail
/// daveti tanır) hem de .ics eki olarak eklenir.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task Send(EmailEnvelope email, CancellationToken ct)
    {
        var smtp = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(new MailboxAddress(email.ToName ?? string.Empty, email.To));
        message.Subject = email.Subject;

        var body = new BodyBuilder { HtmlBody = email.HtmlBody };
        if (email.CalendarContent is { } calendar)
        {
            var method = email.CalendarMethod == CalendarMethod.Cancel ? "CANCEL" : "REQUEST";
            var attachment = body.Attachments.Add("davet.ics", System.Text.Encoding.UTF8.GetBytes(calendar), new ContentType("text", "calendar"));
            attachment.ContentType.Parameters.Add("method", method);
            attachment.ContentType.Charset = "utf-8";
        }
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        var security = smtp.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
        await client.ConnectAsync(smtp.Host, smtp.Port, security, ct);
        if (!string.IsNullOrWhiteSpace(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
