using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Foresight.Api.Mail;

/// <summary>The "Mail:Smtp" section: the mail server the host names.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Mail:Smtp";

    // Empty means no server: every mail goes into the folder instead.
    public string Host { get; set; } = "";

    // The port for mail that programs hand in, encrypted with STARTTLS.
    public int Port { get; set; } = 587;

    public string UserName { get; set; } = "";

    // Only from the host: user secrets on your machine, the host's own
    // settings in production. Never in a file that is committed.
    public string Password { get; set; } = "";

    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Foresight";
}

/// <summary>
/// Sends each mail through the mail server the host names. One connection
/// per mail: Foresight sends few mails, and a connection kept open would
/// need care of its own when the server closes it.
/// </summary>
public sealed class SmtpMailSender : IMailSender
{
    private readonly SmtpOptions _options;

    public SmtpMailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string to, string subject, string html, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(
            new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = html };

        using var client = new SmtpClient();
        // StartTls, not Auto: Auto goes on without encryption when the
        // server offers none, and the password would then cross the
        // network readable. StartTls stops with an error instead.
        await client.ConnectAsync(_options.Host, _options.Port,
            SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(
            _options.UserName, _options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
