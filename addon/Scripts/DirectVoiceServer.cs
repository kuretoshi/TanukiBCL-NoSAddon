using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TanukiBCL.Roles;

// Only amplitude and microphone availability. No audio, game commands or Unity calls on worker threads.
public sealed class DirectVoiceServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly X509Certificate2 certificate;
    private readonly CancellationTokenSource stop = new();
    private readonly object sync = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly HashSet<TcpClient> clients = new();
    private bool paired;
    private long serial;
    private double receivedAt = double.NegativeInfinity;
    private double rms;
    private bool active;
    public readonly byte PlayerId;
    public readonly string Token = Guid.NewGuid().ToString("N");
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string CertificatePath { get; }
    public string LastError { get; private set; } = "";

    public DirectVoiceServer(byte playerId, string directory, int port = 57742)
    {
        PlayerId = playerId;
        Directory.CreateDirectory(directory);
        CertificatePath = Path.Combine(directory, "direct-voice.cer");
        certificate = LoadCertificate(directory);
        listener = new TcpListener(IPAddress.Any, port);
        try { listener.Start(8); }
        catch { certificate.Dispose(); throw; }
        _ = Accept();
    }

    private X509Certificate2 LoadCertificate(string directory)
    {
        string path = Path.Combine(directory, "direct-voice.pfx");
        if (File.Exists(path))
        {
            var existing = new X509Certificate2(path, "", X509KeyStorageFlags.UserKeySet);
            File.WriteAllBytes(CertificatePath, existing.Export(X509ContentType.Cert));
            return existing;
        }
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Tanuki NoS Direct Voice", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
            if (address.AddressFamily == AddressFamily.InterNetwork) san.AddIpAddress(address);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyCertSign, true));
        var usages = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        byte[] pfx = generated.Export(X509ContentType.Pfx, "");
        File.WriteAllBytes(path, pfx);
        File.WriteAllBytes(CertificatePath, generated.Export(X509ContentType.Cert));
        return new X509Certificate2(pfx, "", X509KeyStorageFlags.UserKeySet);
    }

    private async Task Accept()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync();
                bool accepted;
                lock (sync) { accepted = clients.Count < 8; if (accepted) clients.Add(client); }
                if (!accepted) { client.Dispose(); continue; }
                _ = Serve(client);
            }
        }
        catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException) { }
    }

    private async Task Serve(TcpClient client)
    {
        bool ownsPair = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var stream = new SslStream(client.GetStream(), false);
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, timeout.Token);
            string header = await ReadHeader(stream, timeout.Token);
            string? key = Header(header, "Sec-WebSocket-Key");
            if (!header.StartsWith("GET /voice HTTP/1.1\r\n", StringComparison.Ordinal) || key == null)
            {
                await SendHttp(stream, "200 OK", "Tanuki NoS direct voice endpoint. Use the connection address shown in your own NoS settings.", timeout.Token);
                return;
            }
            if (!string.Equals(Header(header, "Upgrade"), "websocket", StringComparison.OrdinalIgnoreCase)
                || Header(header, "Sec-WebSocket-Version") != "13" || Convert.FromBase64String(key).Length != 16) return;
            string accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            byte[] upgrade = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
            await stream.WriteAsync(upgrade, timeout.Token);

            if (await Receive(stream, timeout.Token) != "TBCLW1|" + Token + "|" + PlayerId) return;
            lock (sync) { if (!paired) { paired = true; ownsPair = true; } }
            if (!ownsPair) return;
            timeout.CancelAfter(Timeout.InfiniteTimeSpan);
            await Pump(stream, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException || ex is OperationCanceledException
            || ex is System.Security.Authentication.AuthenticationException || ex is FormatException || ex is ObjectDisposedException) { LastError = ex.Message; }
        finally
        {
            lock (sync)
            {
                clients.Remove(client);
                if (ownsPair) { paired = false; receivedAt = double.NegativeInfinity; }
            }
            client.Dispose();
        }
    }

    private async Task Pump(Stream stream, CancellationToken token)
    {
        long sequence = -1;
        double previous = double.NegativeInfinity;
        while (!token.IsCancellationRequested)
        {
            string ticket = Guid.NewGuid().ToString("N");
            double issued = clock.Elapsed.TotalSeconds;
            byte[] ack = Encoding.ASCII.GetBytes("TBCLA1|" + ticket + "|" + PlayerId);
            await SendFrame(stream, ack, token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            string[] parts = (await Receive(stream, timeout.Token)).Split('|');
            double now = clock.Elapsed.TotalSeconds;
            if (!ParseSample(parts, ticket, sequence, out long seq, out double value, out bool enabled)) return;
            sequence = seq;
            // A server-issued ticket makes queued samples expire without trusting the phone's clock.
            if (now - issued > .75 || now - previous < .03) continue;
            previous = now;
            lock (sync) { serial++; rms = value; active = enabled; receivedAt = now; }
        }
    }

    private static bool ParseSample(string[] parts, string ticket, long previous, out long seq, out double value, out bool enabled)
    {
        seq = -1; value = 0; enabled = false;
        if (parts.Length != 6 || parts[0] != "TBCLS1" || parts[1] != ticket
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out seq) || seq <= previous
            || parts[3] != "1" || !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || !double.IsFinite(value) || value < 0 || value > 1 || (parts[5] != "0" && parts[5] != "1")) return false;
        enabled = parts[5] == "1";
        return true;
    }

    public bool TryRead(byte player, ref long consumed, out double value, out bool enabled)
    {
        lock (sync)
        {
            value = rms; enabled = active;
            if (player != PlayerId || serial <= consumed || clock.Elapsed.TotalSeconds - receivedAt > .15) return false;
            consumed = serial;
            return true;
        }
    }
    public bool OwnsInput(byte player) { lock (sync) return player == PlayerId && paired; }
    private static async Task<string> ReadHeader(Stream stream, CancellationToken token)
    {
        var bytes = new List<byte>();
        byte[] one = new byte[1];
        while (bytes.Count < 8192)
        {
            if (await stream.ReadAsync(one, token) == 0) throw new IOException("Closed handshake");
            bytes.Add(one[0]);
            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.ToArray()).EndsWith("\r\n\r\n", StringComparison.Ordinal))
                return Encoding.ASCII.GetString(bytes.ToArray());
        }
        throw new IOException("Oversized handshake");
    }
    private static string? Header(string request, string name)
    {
        foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
            if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase)) return line.Substring(name.Length + 1).Trim();
        return null;
    }
    private static async Task SendHttp(Stream stream, string status, string body, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
    }
    private static async Task ReadExactly(Stream stream, byte[] bytes, CancellationToken token)
    {
        int offset = 0;
        while (offset < bytes.Length)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(offset), token);
            if (count == 0) throw new IOException("Closed WebSocket");
            offset += count;
        }
    }
    private static async Task SendFrame(Stream stream, byte[] payload, CancellationToken token)
    {
        byte[] frame = new byte[payload.Length + 2];
        frame[0] = 0x81; frame[1] = (byte)payload.Length;
        Array.Copy(payload, 0, frame, 2, payload.Length);
        await stream.WriteAsync(frame, token);
    }
    private static async Task<string> Receive(Stream stream, CancellationToken token)
    {
        byte[] header = new byte[2];
        await ReadExactly(stream, header, token);
        // This narrow protocol permits only complete, masked UTF-8 text frames.
        if (header[0] != 0x81 || (header[1] & 0x80) == 0) throw new IOException("Invalid frame flags");
        int length = header[1] & 0x7f;
        if (length == 127) throw new IOException("Oversized frame");
        if (length == 126)
        {
            byte[] extended = new byte[2];
            await ReadExactly(stream, extended, token);
            length = extended[0] * 256 + extended[1];
        }
        if (length > 256) throw new IOException("Oversized sample");
        byte[] mask = new byte[4], payload = new byte[length];
        await ReadExactly(stream, mask, token);
        await ReadExactly(stream, payload, token);
        for (int i = 0; i < length; i++) payload[i] ^= mask[i % 4];
        return Encoding.ASCII.GetString(payload);
    }
    public void Dispose()
    {
        stop.Cancel(); listener.Stop();
        lock (sync) { foreach (var client in clients) client.Dispose(); paired = false; receivedAt = double.NegativeInfinity; }
        // Let cancelled TLS handshakes finish before releasing their certificate.
        _ = Task.Run(async () => { await Task.Delay(6000); certificate.Dispose(); stop.Dispose(); });
    }
}
