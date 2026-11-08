using System.Text;
using Microsoft.Extensions.Options;

namespace Foresight.Api.Mail;

/// <summary>Sends one mail.</summary>
public interface IMailSender
{
    Task SendAsync(
        string to, string subject, string html, CancellationToken ct = default);
}

/// <summary>The "Mail" section of the settings.</summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    // Where FolderMailSender puts each mail. Empty means "mail" in the data
    // folder.
    public string Folder { get; set; } = "";
}

/// <summary>
/// Writes each mail as a file instead of sending it, so you can build and
/// test every mail without a mail server. Open the .eml file in any mail
/// program. Chapter 6 sends real mail over SMTP on the host.
/// </summary>
public sealed class FolderMailSender : IMailSender
{
    private readonly string _folder;
    private readonly TimeProvider _clock;

    public FolderMailSender(IOptions<MailOptions> options, TimeProvider clock)
    {
        _folder = options.Value.Folder;
        _clock = clock;
        Directory.CreateDirectory(_folder);
    }

    public Task SendAsync(
        string to, string subject, string html, CancellationToken ct = default)
    {
        var name = $"{_clock.GetUtcNow():yyyyMMdd-HHmmss-fff}-"
            + $"{Guid.NewGuid():N}.eml";
        var mail = new StringBuilder()
            .Append("To: ").AppendLine(to)
            .Append("Subject: ").AppendLine(subject)
            .AppendLine("Content-Type: text/html; charset=utf-8")
            .AppendLine()
            .AppendLine(html)
            .ToString();
        return File.WriteAllTextAsync(
            Path.Combine(_folder, name), mail, Encoding.UTF8, ct);
    }
}
