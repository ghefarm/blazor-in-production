using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Foresight.Scheduler;

/// <summary>
/// The "Smtp" section, with the keys of the API's "Mail:Smtp" section
/// (chapter 6), so its values can be copied from there. The password
/// comes only from the host.
/// </summary>
public sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Foresight scheduler";
}

/// <summary>
/// Sends the alarm over SMTP, from the scheduler itself and not through
/// the API: the API being down is one of the things the alarm reports.
/// </summary>
public sealed class SmtpAlarmMail : IAlarmMail
{
    private readonly SmtpSettings _smtp;
    private readonly string _to;

    public SmtpAlarmMail(SmtpSettings smtp, string to)
    {
        _smtp = smtp;
        _to = to;
    }

    public async Task SendAsync(
        string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(_to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        // StartTls, as in the API's sender: never a password in clear text.
        await client.ConnectAsync(
            _smtp.Host, _smtp.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_smtp.UserName, _smtp.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
