using MailKit.Net.Smtp;
using MailKit.Security;

namespace Foresight.Api.Tests;

/// <summary>
/// Facts about MailKit that the SMTP sender relies on: what each way of
/// connecting does with a mail server that offers no encryption.
/// </summary>
[Trait("Chapter", "6")]
public sealed class MailKitFacts
{
    [Fact]
    public async Task Auto_SignsInWithoutEncryption()
    {
        using var server = PlainSmtpServer.Start();
        using var client = new SmtpClient();

        await client.ConnectAsync("127.0.0.1", server.Port,
            SecureSocketOptions.Auto);
        await client.AuthenticateAsync("foresight", "the-password");

        // Nothing was encrypted, and the server could read the password.
        Assert.False(client.IsSecure);
        Assert.Contains("the-password", server.Credentials());
    }

    [Fact]
    public async Task StartTls_StopsBeforeThePasswordIsSent()
    {
        using var server = PlainSmtpServer.Start();
        using var client = new SmtpClient();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            client.ConnectAsync("127.0.0.1", server.Port,
                SecureSocketOptions.StartTls));
        Assert.Null(server.Credentials());
    }
}
