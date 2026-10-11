using System.Net.WebSockets;
using System.Text;
using TanukiBCL.Roles;

public static class DirectVoiceTests
{
    public static async Task Serve()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tanuki-direct-integration-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var server = new DirectVoiceServer(7, directory, 0);
            DirectVoiceEndpoint.Current = server;
            using var bridge = new SharedVoiceBridge(7, directory, 0);
            Console.WriteLine($"READY|wss://127.0.0.1:{server.Port}/voice#{server.Token}");
            var end = Task.Run(() => Console.In.ReadLine());
            while (!end.IsCompleted)
            {
                if (bridge.TryReadSample(out _, out double rms, out bool active))
                    Console.WriteLine("SAMPLE|" + rms.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (active ? "1" : "0"));
                await Task.Delay(10);
            }
        }
        finally { DirectVoiceEndpoint.Current = null; Directory.Delete(directory, true); }
    }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    private static async Task Send(ClientWebSocket ws, string message)
        => await ws.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
    private static async Task<string> Read(ClientWebSocket ws)
    {
        using var timeout = new CancellationTokenSource(3000);
        byte[] bytes = new byte[256];
        var frame = await ws.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
        return Encoding.ASCII.GetString(bytes, 0, frame.Count);
    }
    private static async Task<ClientWebSocket> Open(DirectVoiceServer server, string? token = null, byte player = 7)
    {
        var ws = new ClientWebSocket();
        // Only this loopback test accepts its freshly generated test certificate.
        ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await ws.ConnectAsync(new Uri($"wss://localhost:{server.Port}/voice"), CancellationToken.None);
        await Send(ws, $"TBCLW1|{token ?? server.Token}|{player}");
        return ws;
    }
    private static async Task AssertRejected(ClientWebSocket ws, string reason)
    {
        try { var data = await Read(ws); Check(!data.StartsWith("TBCLA1|"), reason); }
        catch (WebSocketException) { }
    }
    private static async Task Sample(ClientWebSocket ws, string ack, long sequence, double rms, bool active)
    {
        await Task.Delay(50);
        string ticket = ack.Split('|')[1];
        await Send(ws, $"TBCLS1|{ticket}|{sequence}|1|{rms.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{(active ? 1 : 0)}");
    }
    public static async Task Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tanuki-direct-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var server = new DirectVoiceServer(7, directory, 0);
            ClientWebSocket wrong;
            try { wrong = await Open(server, new string('a', 32)); }
            catch { throw new Exception("Server TLS: " + server.LastError); }
            using (wrong) await AssertRejected(wrong, "wrong secret rejected");
            using (var wrongPlayer = await Open(server, player: 8)) await AssertRejected(wrongPlayer, "wrong player rejected");
            DirectVoiceEndpoint.Current = server;
            using var bridge = new SharedVoiceBridge(7, directory, 0);
            using var secondReader = new SharedVoiceBridge(7, directory, 0);
            using var phone = await Open(server);
            string ack = await Read(phone);
            Check(ack.StartsWith("TBCLA1|"), "authenticated direct socket");
            using (var duplicate = await Open(server)) await AssertRejected(duplicate, "second microphone rejected");
            // TLS setup for the duplicate may exceed the first ticket's lifetime.
            await Sample(phone, ack, 0, 0, false);
            ack = await Read(phone);
            await Sample(phone, ack, 1, .5, true);
            ack = await Read(phone);
            Check(bridge.TryReadSample(out var seq, out var rms, out var active) && rms == .5 && active, "direct RMS reaches role bridge");
            Check(secondReader.TryReadSample(out var otherSeq, out var otherRms, out _) && seq == otherSeq && rms == otherRms, "modifiers share direct sample");
            Check(bridge.Connected, "direct bridge connected");
            await Sample(phone, ack, 2, .4, false);
            ack = await Read(phone);
            Check(bridge.TryReadSample(out _, out _, out active) && !active, "explicit mute reaches role bridge");
            await Task.Delay(800);
            await Sample(phone, ack, 3, 1, true);
            ack = await Read(phone);
            Check(!bridge.TryReadSample(out _, out _, out _), "expired ticket cannot drive death");
            await Sample(phone, ack, 4, .7, true);
            ack = await Read(phone);
            Check(bridge.TryReadSample(out _, out rms, out active) && rms == .7 && active, "fresh ticket recovers");
            await Sample(phone, ack, 4, 1, true);
            await AssertRejected(phone, "replayed sequence rejected");
            await Task.Delay(1100);
            Check(!bridge.Connected, "disconnect expires role input");
            Check(File.Exists(server.CertificatePath), "public certificate exported separately");
            using var reconnect = await Open(server);
            ack = await Read(reconnect);
            await Sample(reconnect, ack, 1, .2, true);
            ack = await Read(reconnect);
            Check(bridge.TryReadSample(out _, out rms, out _) && rms == .2, "new socket resets wire sequence safely");
            await Sample(reconnect, ack, 2, double.NaN, true);
            await AssertRejected(reconnect, "invalid RMS rejected");
            Console.WriteLine("Direct WSS: TLS, secret/player binding, one microphone, shared role feed, mute, ticket expiry, replay, reconnect and invalid RMS checks passed.");
        }
        finally { DirectVoiceEndpoint.Current = null; Directory.Delete(directory, true); }
    }
}
