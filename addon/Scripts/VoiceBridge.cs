using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TanukiBCL.Roles;

// One endpoint per local role activation; no remote audio or game commands are accepted.
public sealed class VoiceBridge : IDisposable
{
    private readonly UdpClient socket;
    private readonly string token = Guid.NewGuid().ToString("N");
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly LoudnessGate gate = new();
    private readonly string descriptorPath;
    private readonly string descriptor;
    private long sequence = -1;
    private long directConsumed = -1;
    private long hostConsumed = -1;
    private DirectVoiceServer? lastDirect;
    private long delivered = -1;
    private double nextPublish;
    private UdpClient? discovery;
    private double nextDiscovery;
    private readonly int discoveryPort;
    private readonly byte playerId;
    private bool receivedFirstSample;
    private double lastSample = double.NegativeInfinity;
    public double LatestRms { get; private set; }
    public bool LatestActive { get; private set; }
    public bool LatestFromHostRelay { get; private set; }
    public bool Connected => clock.Elapsed.TotalSeconds - lastSample <= 1;
    public string DescriptorPath => descriptorPath;

    public VoiceBridge(byte playerId, string role = "tanukiBclBibiri", string? directory = null, int discoveryPort = 57741)
    {
        this.discoveryPort = discoveryPort;
        this.playerId = playerId;
        socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        socket.Client.Blocking = false;
        // Use the same environment variable as BetterCrewLink, including under
        // the game's embedded runtime where special-folder resolution can differ.
        directory ??= Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS");
        Directory.CreateDirectory(directory);
        descriptorPath = Path.Combine(directory, "voice-role.json");
        int port = ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
        descriptor = "{\"protocol\":1,\"role\":\"" + role + "\",\"playerId\":" + playerId +
            ",\"pid\":" + Environment.ProcessId + ",\"port\":" + port + ",\"token\":\"" + token + "\"}";
        try
        {
            Publish();
        }
        catch { socket.Dispose(); throw; }
    }

    private void Publish()
    {
        // Verify the result rather than assuming a successful Move replaced it.
        // A short direct write is safe here: the sender retries partial JSON.
        using (var stream = new System.IO.FileStream(descriptorPath, System.IO.FileMode.Create,
            System.IO.FileAccess.Write, System.IO.FileShare.Read))
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(descriptor);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        if (System.IO.File.ReadAllText(descriptorPath) != descriptor)
            throw new IOException("Voice endpoint write verification failed at " + descriptorPath);
    }

    public bool Poll(bool eligible, double thresholdDb, double durationSeconds)
    {
        if (!eligible) gate.Reset();
        if (!TryReadSample(out long seq, out double rms, out bool active)) return false;
        return eligible && gate.Sample(seq, rms, active, clock.Elapsed.TotalSeconds, thresholdDb, durationSeconds);
    }

    public bool TryReadSample(out long seq, out double rms, out bool active)
    {
        seq = -1;
        rms = 0;
        active = false;
        if (HostVoiceRelay.TryRead(playerId, ref hostConsumed, out rms, out active))
        {
            LatestFromHostRelay = true;
            seq = ++delivered;
            LatestRms = rms; LatestActive = active; lastSample = clock.Elapsed.TotalSeconds;
            return true;
        }
        var direct = DirectVoiceEndpoint.Current;
        if (direct != lastDirect)
        {
            lastDirect = direct;
            directConsumed = -1;
            lastSample = double.NegativeInfinity;
            gate.Reset();
        }
        if (direct != null && direct.PlayerId == playerId)
        {
            if (!direct.TryRead(playerId, ref directConsumed, out rms, out active)) return false;
            LatestFromHostRelay = false;
            seq = ++delivered;
            LatestRms = rms; LatestActive = active; lastSample = clock.Elapsed.TotalSeconds;
            return true;
        }
        Discover();
        if (clock.Elapsed.TotalSeconds >= nextPublish)
        {
            nextPublish = clock.Elapsed.TotalSeconds + 1;
            // Repair deletion/replacement while this local role is still active.
            // Descriptor discovery is maintenance, not the lifetime of the UDP
            // session. A temporary file lock must not replace the socket/token.
            try
            {
                if (!File.Exists(descriptorPath) || File.ReadAllText(descriptorPath) != descriptor) Publish();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        string? latest = null;
        // Bound work per frame. Discard a backlog instead of treating it as continuous speech.
        for (int i = 0; i < 64 && socket.Available > 0; i++)
        {
            IPEndPoint sender = new(IPAddress.Any, 0);
            byte[] bytes = socket.Receive(ref sender);
            if (bytes.Length <= 256 && IPAddress.IsLoopback(sender.Address))
                latest = Encoding.ASCII.GetString(bytes);
        }
        if (socket.Available > 0) { gate.Reset(); return false; }
        if (latest == null) return false;
        string[] parts = latest.Split('|');
        if (parts.Length != 6 || parts[0] != "TBCL1" || parts[1] != token ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out seq) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long unixMs) ||
            !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out rms) ||
            (parts[5] != "0" && parts[5] != "1"))
        { gate.Reset(); return false; }
        long age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - unixMs;
        if (age < -100 || age > 150) { gate.Reset(); return false; }
        if (seq <= sequence || !double.IsFinite(rms) || rms < 0 || rms > 1) return false;
        sequence = seq;
        delivered = Math.Max(delivered + 1, seq);
        seq = delivered;
        active = parts[5] == "1";
        LatestRms = rms;
        LatestFromHostRelay = false;
        LatestActive = active;
        lastSample = clock.Elapsed.TotalSeconds;
        if (!receivedFirstSample)
        {
            receivedFirstSample = true;
            System.Console.WriteLine("[TanukiBCL] Voice sample received for PID " + Environment.ProcessId);
        }
        return true;
    }

    private void Discover()
    {
        if (discovery == null && clock.Elapsed.TotalSeconds >= nextDiscovery)
        {
            nextDiscovery = clock.Elapsed.TotalSeconds + 1;
            try
            {
                discovery = new UdpClient(new IPEndPoint(IPAddress.Loopback, discoveryPort));
                discovery.Client.Blocking = false;
            }
            catch (SocketException) { discovery?.Dispose(); discovery = null; }
        }
        if (discovery == null) return;
        for (int i = 0; i < 8 && discovery.Available > 0; i++)
        {
            IPEndPoint sender = new(IPAddress.Any, 0);
            byte[] request = discovery.Receive(ref sender);
            if (!IPAddress.IsLoopback(sender.Address) || request.Length > 128) continue;
            string[] parts = Encoding.ASCII.GetString(request).Split('|');
            if (parts.Length != 3 || parts[0] != "TBCLD1" || parts[1] != Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
                || parts[2].Length != 32) continue;
            bool hex = true;
            foreach (char c in parts[2]) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) hex = false;
            if (!hex) continue;
            byte[] response = Encoding.ASCII.GetBytes("TBCLD1|" + parts[2] + "|" + descriptor);
            discovery.Send(response, response.Length, sender);
        }
    }

    public void Dispose()
    {
        socket.Dispose();
        discovery?.Dispose(); discovery = null;
        try
        {
            if (File.Exists(descriptorPath) && File.ReadAllText(descriptorPath) == descriptor)
                File.Delete(descriptorPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
