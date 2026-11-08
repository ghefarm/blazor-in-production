using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Foresight.Api.Tests;

/// <summary>
/// A mail server for tests that offers no encryption. It greets, answers
/// EHLO without STARTTLS, takes AUTH PLAIN and remembers what it was sent.
/// It knows just enough of SMTP for a client to connect and sign in.
/// </summary>
public sealed class PlainSmtpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly ConcurrentQueue<string> _received = new();

    private PlainSmtpServer(TcpListener listener)
    {
        _listener = listener;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>Every line a client sent, in order.</summary>
    public IReadOnlyList<string> Received => _received.ToList();

    public static PlainSmtpServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = new PlainSmtpServer(listener);
        _ = server.ServeAsync();
        return server;
    }

    public void Dispose() => _listener.Stop();

    /// <summary>
    /// The user name and password of AUTH PLAIN, decoded: anyone who can
    /// read the connection can do the same.
    /// </summary>
    public string? Credentials()
    {
        var lines = Received.ToList();
        var at = lines.FindIndex(l => l.StartsWith("AUTH PLAIN",
            StringComparison.Ordinal));
        if (at < 0)
            return null;
        var payload = lines[at] == "AUTH PLAIN"
            ? lines.ElementAtOrDefault(at + 1)
            : lines[at]["AUTH PLAIN ".Length..];
        return payload is null
            ? null
            : Encoding.UTF8.GetString(Convert.FromBase64String(payload));
    }

    private async Task ServeAsync()
    {
        try
        {
            while (true)
            {
                using var tcp = await _listener.AcceptTcpClientAsync();
                await TalkAsync(tcp.GetStream());
            }
        }
        catch (Exception e) when (e is SocketException
            or ObjectDisposedException or IOException)
        {
            // The test is over and the listener stopped.
        }
    }

    private async Task TalkAsync(NetworkStream stream)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII)
            { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteLineAsync("220 test ESMTP");
        var credentialsNext = false;
        while (await reader.ReadLineAsync() is { } line)
        {
            _received.Enqueue(line);
            var verb = line.Split(' ')[0].ToUpperInvariant();
            if (credentialsNext)
            {
                credentialsNext = false;
                await writer.WriteLineAsync("235 2.7.0 Signed in");
            }
            else if (verb == "EHLO")
            {
                await writer.WriteLineAsync("250-test");
                await writer.WriteLineAsync("250 AUTH PLAIN");
            }
            else if (line == "AUTH PLAIN")
            {
                // The client sends its credentials on the next line.
                credentialsNext = true;
                await writer.WriteLineAsync("334 ");
            }
            else if (verb == "AUTH")
                await writer.WriteLineAsync("235 2.7.0 Signed in");
            else if (verb == "QUIT")
            {
                await writer.WriteLineAsync("221 Bye");
                return;
            }
            else
                await writer.WriteLineAsync("502 Not here");
        }
    }
}
