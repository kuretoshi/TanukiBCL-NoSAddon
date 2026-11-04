using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TanukiBCL.Link;

// Network workers handle bounded frames only. The game thread consumes commands
// explicitly; no Unity or role API is invoked from a network worker.
public sealed class AddonLinkServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentQueue<JsonElement> commands = new();
    private readonly ConcurrentDictionary<TcpClient, byte> clients = new();
    private readonly int pid;
    private int queued;
    private long acknowledged;
    private long revision;
    private string snapshot = "";
    public readonly string Session = Guid.NewGuid().ToString("N");
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public bool Healthy(long now) => now - Interlocked.Read(ref acknowledged) is >= 0 and <= 500;
    public AddonLinkServer(int processId)
    {
        pid = processId;
        listener.Start(4);
        _ = Accept();
    }
    public void SetSnapshot(string json, long sequence)
    {
        if (Encoding.UTF8.GetByteCount(json) > AddonLinkFramer.Maximum - 1) throw new InvalidOperationException("Addon link state too large");
        Volatile.Write(ref snapshot, json); Interlocked.Exchange(ref revision, sequence);
    }
    public bool TryRead(out JsonElement command)
    {
        if (!commands.TryDequeue(out command)) return false;
        Interlocked.Decrement(ref queued); return true;
    }
    private async Task Accept()
    {
        try
        {
            while (!stop.IsCancellationRequested) Admit(await listener.AcceptTcpClientAsync(stop.Token));
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is ObjectDisposedException || ex is SocketException) { }
    }
    private void Admit(TcpClient client)
    {
        if (clients.Count >= 4) { client.Dispose(); return; }
        clients[client] = 0; client.NoDelay = true;
        _ = Serve(client);
    }
    private async Task Serve(TcpClient client)
    {
        try
        {
            var frames = new AddonLinkFramer();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var peer = new Peer();
            while (!stop.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, stop.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(3), stop.Token);
                if (read == 0) break;
                await DispatchFrames(stream, frames.Push(buffer.AsSpan(0, read)), peer);
            }
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException || ex is OperationCanceledException ||
            ex is ObjectDisposedException || ex is JsonException || ex is InvalidOperationException || ex is TimeoutException || ex is DecoderFallbackException) { }
        finally { clients.TryRemove(client, out _); client.Dispose(); }
    }
    private sealed class Peer { public bool Hello; public long Sequence; }
    private async Task DispatchFrames(NetworkStream stream, System.Collections.Generic.List<string> frames, Peer peer)
    {
        foreach (string frame in frames)
        {
            string? reply = Handle(frame, peer);
            if (reply != null) await stream.WriteAsync(Encoding.UTF8.GetBytes(reply + "\n"), stop.Token);
        }
    }
    private string? Handle(string frame, Peer peer)
    {
        using var document = JsonDocument.Parse(frame, new JsonDocumentOptions { MaxDepth = 16 });
        var m = document.RootElement;
        if (m.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Invalid addon link envelope");
        if (!peer.Hello) return Hello(m, peer);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!AddonLinkWire.Valid(m, pid, Session, now, ref peer.Sequence)) return null;
        string? kind = AddonLinkWire.Text(m, "kind");
        if (stop.IsCancellationRequested) return null;
        if (kind == "poll")
        {
            long latest = Interlocked.Read(ref revision);
            if (AddonLinkWire.Integer(m, "accepted", out long ack) && ack >= Math.Max(1, latest - 10) && ack <= latest)
                Interlocked.Exchange(ref acknowledged, now);
            string state = Volatile.Read(ref snapshot);
            return state.Length > 0 ? state : null;
        }
        if (kind is not ("sample" or "ready" or "word")) return null;
        if (Interlocked.Increment(ref queued) <= 128) commands.Enqueue(m.Clone());
        else Interlocked.Decrement(ref queued);
        return null;
    }
    private string Hello(JsonElement m, Peer peer)
    {
        string? nonce = AddonLinkWire.Text(m, "nonce");
        if (AddonLinkWire.Text(m, "kind") != "hello" || !AddonLinkWire.Hex(nonce) ||
            !AddonLinkWire.Integer(m, "pid", out long target) || target != pid ||
            !AddonLinkWire.Integer(m, "protocol", out long protocol) || protocol != 1)
            throw new InvalidOperationException("Invalid addon link hello");
        peer.Hello = true;
        return JsonSerializer.Serialize(new { kind = "hello", protocol = 1, pid, nonce, session = Session });
    }
    public void Dispose()
    {
        stop.Cancel(); listener.Stop();
        foreach (var client in clients.Keys) client.Dispose();
        while (TryRead(out _)) { }
    }
}
