using TanukiBCL.Roles;

if (args.Length == 1 && args[0] == "--direct-test-server") { await DirectVoiceTests.Serve(); return; }

static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
static bool Sample(LoudnessGate gate, long seq, double time, double rms = .5, bool active = true)
    => gate.Sample(seq, rms, active, time, -12, .5);

var speechBudget = new SpeechBudget();
string sanitizedReport = InquiryReport.Redact(@"C:\Users\alice\game Bearer abc123 token=secret123 github_pat_example https://discord.com/api/webhooks/123/sensitive");
Check(!sanitizedReport.Contains("alice") && !sanitizedReport.Contains("abc123") && !sanitizedReport.Contains("secret123") && !sanitizedReport.Contains("github_pat_example") && !sanitizedReport.Contains("sensitive"), "Inquiry log sanitizer removes common credentials and profile paths");
Check(InquiryReport.Redact("Ordinary NoS error: failed to load role") == "Ordinary NoS error: failed to load role", "Inquiry log sanitizer preserves diagnostic text");
var reportFolder = Path.Combine(Path.GetTempPath(), "TanukiInquiry-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(reportFolder);
File.WriteAllText(Path.Combine(reportFolder, "NebulaLog.txt"), new string('x', 300000) + "\nLAST ERROR token=private123");
var reportLogs = InquiryReport.Collect(reportFolder);
Check(reportLogs.Length == 2 && reportLogs[0].text.Contains("LAST ERROR") && !reportLogs[0].text.Contains("private123") && reportLogs[0].text.Length <= 262200, "Inquiry attaches only bounded sanitized log tails");
Check(reportLogs[1].text == "[Log unavailable]", "Inquiry handles missing logs without aborting submission");
File.Delete(Path.Combine(reportFolder, "NebulaLog.txt"));
Directory.Delete(reportFolder);
speechBudget.Sample(.5, true, 0, -35, 60);
for (int i = 1; i <= 10; i++) speechBudget.Sample(.5, true, i * .1, -35, 60);
Check(Math.Abs(speechBudget.UsedSeconds - 1) < 1e-9, "LimitSpeech counts spoken intervals");
for (int i = 11; i <= 20; i++) speechBudget.Sample(0, true, i * .1, -35, 60);
Check(Math.Abs(speechBudget.UsedSeconds - 1) < 1e-9, "LimitSpeech pauses while silent");
speechBudget.Sample(.5, false, 2.1, -35, 60);
speechBudget.Sample(.5, false, 2.2, -35, 60);
Check(Math.Abs(speechBudget.UsedSeconds - 1) < 1e-9, "LimitSpeech pauses while muted");
speechBudget.Pause(); speechBudget.Sample(.5, true, 50, -35, 60);
Check(Math.Abs(speechBudget.UsedSeconds - 1) < 1e-9, "LimitSpeech reconnect does not spend missing time");
speechBudget.Sample(.5, true, 50.1, -35, 60);
speechBudget.Sample(.5, true, 55, -35, 60);
Check(Math.Abs(speechBudget.UsedSeconds - 1.1) < 1e-9, "LimitSpeech ignores stalled sample intervals");
var shortBudget = new SpeechBudget();
shortBudget.Sample(.5, true, 0, -35, .1);
Check(shortBudget.Sample(.5, true, .1, -35, .1), "LimitSpeech expires at the budget boundary");
Check(shortBudget.Remaining(.1) == 0, "LimitSpeech remaining time never goes negative");
Check(shortBudget.Add(10) && Math.Abs(shortBudget.Remaining(.1) - 10) < 1e-9, "LimitSpeech reward restores ten seconds after exhaustion");
shortBudget.Sample(.5, true, 20, -35, .1);
Check(Math.Abs(shortBudget.Remaining(.1) - 10) < 1e-9, "LimitSpeech reward pauses the old sample interval");
var rewardedBudget = new SpeechBudget();
Check(rewardedBudget.Add(10) && rewardedBudget.Add(20) && rewardedBudget.Remaining(60) == 90, "LimitSpeech task and kill rewards accumulate above initial budget");
Check(!rewardedBudget.Add(0) && !rewardedBudget.Add(-10) && !rewardedBudget.Add(double.NaN) && !rewardedBudget.Add(double.PositiveInfinity) && rewardedBudget.Remaining(60) == 90, "LimitSpeech disabled or invalid rewards do not change time");
var overshotBudget = new SpeechBudget();
overshotBudget.Sample(.5, true, 0, -35, .05);
Check(overshotBudget.Sample(.5, true, .1, -35, .05) && overshotBudget.Add(10) && Math.Abs(overshotBudget.Remaining(.05) - 10) < 1e-9, "LimitSpeech sampling overshoot does not reduce the next reward");

var instant = new LoudnessGate();
Check(!instant.Sample(0, .1, true, 0, -12, 0), "instant Bibiri ignores quiet voice");
Check(instant.Sample(1, Math.Pow(10, -12d / 20), true, .05, -12, 0), "instant Bibiri kills on first sample at threshold");
Check(!instant.Sample(2, 1, false, .1, -12, 0), "muted samples never kill Bibiri");

var continuous = new LoudnessGate();
for (int i = 0; i < 6; i++) Check(Sample(continuous, i, i * .1) == (i == 5), "continuous threshold");
var gap = new LoudnessGate();
Sample(gap, 0, 0); Sample(gap, 1, .1);
Check(!Sample(gap, 2, .5), "stale samples reset");
var spike = new LoudnessGate();
Sample(spike, 0, 0);
Check(!Sample(spike, 1, .1, .01), "quiet resets");
Check(!Sample(spike, 2, .2), "isolated spike");
Check(!Sample(spike, 3, .3, double.NaN), "NaN rejects");
Check(!Sample(spike, 4, .4, 2), "invalid RMS rejects");
Check(!Sample(spike, 5, .5, .5, false), "muted resets");
spike.Reset();
Check(!Sample(spike, 4, .6), "sequence retained after meeting reset");
Check(!Sample(spike, 6, .7), "new onset after meeting");
Console.WriteLine("LoudnessGate checks passed.");

// Exercise the real UDP transport, including stale/wrong-session packets and cleanup.
var testDirectory = Path.Combine(Path.GetTempPath(), "TanukiBCLTests-" + Guid.NewGuid().ToString("N"));
var path = Path.Combine(testDirectory, "voice-role.json");
int discoveryPort;
using (var reservation = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0)))
    discoveryPort = ((System.Net.IPEndPoint)reservation.Client.LocalEndPoint!).Port;
using (var bridge = new VoiceBridge(7, directory: testDirectory, discoveryPort: discoveryPort))
{
    using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
    var root = json.RootElement;
    int port = root.GetProperty("port").GetInt32();
    string token = root.GetProperty("token").GetString()!;
    File.WriteAllText(path, "{\"pid\":1}");
    Check(!bridge.TryReadSample(out _, out _, out _), "repair does not synthesize speech");
    Check(File.ReadAllText(path) == root.GetRawText(), "stale endpoint restored to current session");
    using (var discoveryClient = new System.Net.Sockets.UdpClient())
    {
        discoveryClient.Client.ReceiveTimeout = 500;
        string nonce = new string('a', 32);
        var request = System.Text.Encoding.ASCII.GetBytes($"TBCLD1|{Environment.ProcessId}|{nonce}");
        discoveryClient.Send(request, request.Length, "127.0.0.1", discoveryPort);
        Thread.Sleep(25);
        bridge.TryReadSample(out _, out _, out _);
        var sender = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
        string response = System.Text.Encoding.ASCII.GetString(discoveryClient.Receive(ref sender));
        Check(response == "TBCLD1|" + nonce + "|" + root.GetRawText(), "discovery returns real session without file lookup");
    }
    using var udp = new System.Net.Sockets.UdpClient();
    long seq = 0;
    void Send(string session, long timestamp)
    {
        string payload = $"TBCL1|{session}|{seq++}|{timestamp}|0.5|1";
        var bytes = System.Text.Encoding.ASCII.GetBytes(payload);
        udp.Send(bytes, bytes.Length, "127.0.0.1", port);
        Thread.Sleep(20);
    }
    Send("wrong-token", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    Check(!bridge.Poll(true, -12, .25), "wrong session rejected");
    Send(token, DateTimeOffset.UtcNow.AddSeconds(-2).ToUnixTimeMilliseconds());
    Check(!bridge.Poll(true, -12, .25), "stale packet rejected");
    bool killed = false;
    for (int i = 0; i < 12; i++)
    {
        Send(token, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        killed |= bridge.Poll(true, -12, .25);
        Thread.Sleep(30);
    }
    Check(killed, "UDP continuous voice triggers");
    Send(token, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    Check(!bridge.Poll(false, -12, .25), "meeting suppresses death");
    Send(token, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    Check(!bridge.Poll(true, -12, .25), "meeting clears accumulated volume");
}
Check(!File.Exists(path), "descriptor removed on release");
Directory.Delete(testDirectory);
Console.WriteLine("UDP bridge checks passed.");

var megaphone = new MegaphoneCharge();
Check(Math.Abs(megaphone.Width(.05, 3.5) - .05) < 1e-9, "unconnected/silent fires minimum width");
for (int i = 0; i <= 20; i++) megaphone.Sample(1, true, i * .05, -45, -12, .5);
Check(megaphone.Energy == 1, "loud voice fills and caps energy");
megaphone.Sample(0, false, 1.05, -45, -12, .5);
Check(megaphone.Energy == 1, "silence preserves accumulated energy");
megaphone.Reset();
Check(megaphone.Energy == 0, "cancellation discards energy");
megaphone.Sample(1, true, 0, -45, -12, 1);
megaphone.Sample(1, true, .05, -45, -12, 1);
double partial = megaphone.Energy;
megaphone.Sample(1, true, 2, -45, -12, 1);
Check(Math.Abs(megaphone.Energy - partial) < 1e-9, "disconnect grants no additional energy");
megaphone.Sample(0, false, 2.05, -45, -12, 1);
Check(megaphone.Energy == partial, "muted does not charge");
var quiet = new MegaphoneCharge();
var relayed = new MegaphoneCharge();
relayed.Sample(1, true, 0, -45, -12, 1, .35);
relayed.Sample(1, true, .2, -45, -12, 1, .35);
Check(Math.Abs(relayed.Energy - .2) < 1e-9, "host relay jitter does not discard continuous speech");
relayed.Sample(1, true, .8, -45, -12, 1, .35);
Check(Math.Abs(relayed.Energy - .2) < 1e-9, "host relay interruption grants no energy");
relayed.Sample(0, false, 1, -45, -12, 1, .35);
Check(Math.Abs(relayed.Energy - .2) < 1e-9, "host relay mute grants no energy");
for (int i = 0; i <= 20; i++) quiet.Sample(.0001, true, i * .05, -45, -12, 1);
Check(quiet.Energy == 0, "below threshold does not charge");
var medium = new MegaphoneCharge();
for (int i = 0; i <= 20; i++) medium.Sample(Math.Pow(10, -28.5 / 20), true, i * .05, -45, -12, 1);
Check(Math.Abs(medium.Energy - .5) < .001, "medium volume accumulates slower");
Check(MegaphoneCharge.Hits(100, .01, 0, .05), "wall-piercing beam has no length cap");
Check(!MegaphoneCharge.Hits(-1, 0, 0, 3.5), "never hits behind weapon");
Check(!MegaphoneCharge.Hits(3, .03, 0, .05), "minimum beam is narrow");
Check(MegaphoneCharge.Hits(3, 1, 0, 3.5), "charged beam is wide");
Check(MegaphoneCharge.Hits(0, 3, Math.PI / 2, .05), "vertical aiming rotates hit geometry");
Check(MegaphoneCharge.Aim(2, 1) == Math.PI, "horizontal aim snaps left");
Check(MegaphoneCharge.Aim(.9, 2) == Math.PI / 2, "cardinal aim snaps up");
Console.WriteLine("Megaphone charge and beam geometry checks passed.");

var silence = new SilenceCountdown();
for (int i = 0; i <= 20; i++)
    Check(silence.Sample(0, true, i * .05, -35, 1) == (i == 20), "silent heartbeat deadline");
silence.Reset();
silence.Sample(0, true, 0, -35, 1);
silence.Sample(0, true, .1, -35, 1);
Check(!silence.Sample(.1, true, .2, -35, 1) && silence.SilentSeconds == 0, "speech resets countdown");
Check(!silence.Sample(.0001, true, .3, -35, 1) && silence.SilentSeconds > 0, "quiet noise is silence");
double left = silence.Remaining(1);
Check(silence.Connected(.55), "brief scheduling stall does not flash disconnected");
Check(silence.Connected(1.3), "connection grace includes one second");
Check(!silence.Connected(1.31), "connection grace expires");
Check(silence.Remaining(1) == left, "UI grace does not accumulate unobserved silence");
Check(!silence.Connected(2), "missing heartbeat displays disconnected");
Check(!silence.Sample(0, true, 2, -35, 1) && silence.Remaining(1) == left, "disconnect freezes instead of killing");
Check(!silence.Sample(1, false, 2.1, -35, 1) && silence.Remaining(1) < left, "connected mute counts as silence");
silence.Pause();
left = silence.Remaining(1);
Check(!silence.Sample(0, true, 5, -35, 1) && silence.Remaining(1) == left, "paused period never counted");
silence.Reset();
Check(silence.Remaining(1) == 1, "meeting disabled resets full duration");
for (int i = 0; i <= 20; i++)
    Check(silence.Sample(0, true, 6 + i * .05, -35, 1) == (i == 20), "meeting enabled uses same silence deadline");
silence.Reset();
Check(!silence.Sample(double.NaN, true, 0, -35, 1) && !silence.Connected(0), "invalid sample cannot establish connection");
Console.WriteLine("Talkative silence, speech, mute, disconnect and meeting countdown checks passed.");

// Validate and export the exact samples used by Unity, rather than a mock sound.
var names = new[] { "charge-start", "charge-loop", "fire", "cancel", "beam-loop" };
for (int kind = 0; kind < names.Length; kind++)
{
    var samples = MegaphoneSynth.Create(kind);
    Check(samples.All(s => float.IsFinite(s) && Math.Abs(s) <= .9f), "audio finite/headroom " + names[kind]);
    Check(samples.Any(s => Math.Abs(s) > .1f), "audio not silent " + names[kind]);
    Check(Math.Abs(samples[0]) < .001f && Math.Abs(samples[^1]) < .025f, "audio endpoint continuity " + names[kind]);
    if (args.Length == 2 && args[0] == "--sound-preview")
    {
        Directory.CreateDirectory(args[1]);
        using var writer = new BinaryWriter(File.Create(Path.Combine(args[1], "megaphone-" + names[kind] + ".wav")));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(MegaphoneSynth.Rate);
        writer.Write(MegaphoneSynth.Rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
        foreach (float sample in samples) writer.Write((short)Math.Round(sample * short.MaxValue));
    }
}
Console.WriteLine("Original sound sample/headroom/loop boundary checks passed.");

var sharedDir = Path.Combine(Path.GetTempPath(), "TanukiShared-" + Guid.NewGuid().ToString("N"));
var sharedPath = Path.Combine(sharedDir, "voice-role.json");
using (var first = new SharedVoiceBridge(7, sharedDir, discoveryPort))
{
    using var second = new SharedVoiceBridge(7, sharedDir, discoveryPort);
    using var third = new SharedVoiceBridge(7, sharedDir, discoveryPort);
    using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(sharedPath));
    var root = json.RootElement;
    Check(root.GetProperty("role").GetString() == "tanukiVoiceRoles", "shared transport identity");
    using var udp = new System.Net.Sockets.UdpClient();
    var bytes = System.Text.Encoding.ASCII.GetBytes($"TBCL1|{root.GetProperty("token").GetString()}|1|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}|0.5|1");
    udp.Send(bytes, bytes.Length, "127.0.0.1", root.GetProperty("port").GetInt32());
    Thread.Sleep(20);
    Check(first.Poll(true, -12, 0), "modifier one receives and independently dies");
    Check(second.TryReadSample(out var seq, out var rms, out var active) && seq == 1 && rms == .5 && active, "modifier two receives same sample");
    Check(third.TryReadSample(out _, out _, out _), "megaphone also receives same sample");
    Check(!second.TryReadSample(out _, out _, out _), "a consumer never receives duplicates");
    first.Dispose();
    Check(File.Exists(sharedPath) && second.Connected, "removing one modifier keeps other consumers connected");
}
Check(!File.Exists(sharedPath), "final consumer releases shared endpoint");
Directory.Delete(sharedDir);
Console.WriteLine("Shared modifier and megaphone transport checks passed.");

var battery = new MegaphoneBattery();
Check(battery.Charge == 1 && !battery.Active, "megaphone starts full and switched off");
battery.Update(30, 60, true);
Check(battery.Charge == 1, "switched-off battery does not drain");
Check(!battery.Toggle(false) && !battery.Active, "cannot activate during meeting");
Check(battery.Toggle(true), "full battery activates");
battery.Update(30, 60, true);
Check(battery.Charge == .5 && battery.Active, "default sixty seconds gives thirty seconds at half charge");
battery.Update(10, 60, false);
Check(battery.Charge == .5 && !battery.Active, "meeting turns off without draining or resetting charge");
battery.Recharge(10);
Check(Math.Abs(battery.Charge - .6) < 1e-9 && !battery.Active, "task restores ten percent and does not auto-activate");
battery.Toggle(true);
battery.Update(100, 60, true);
Check(battery.Charge == 0 && !battery.Active, "exhaustion stops amplifier and clamps charge");
Check(!battery.Toggle(true), "empty battery cannot activate");
battery.Recharge(10);
Check(battery.Toggle(true), "a task enables an empty amplifier again");
battery.Update(6, 60, true);
Check(battery.Charge == 0 && !battery.Active, "ten percent yields six seconds of default use");
battery.Recharge(100);
battery.Recharge(10);
Check(battery.Charge == 1, "recharge never exceeds full battery");
battery.Toggle(true);
battery.Update(15, 30, true);
Check(battery.Charge == .5, "host-configured duration changes consumption rate");
battery.Update(double.NaN, 30, true);
battery.Recharge(double.NaN);
Check(battery.Charge == .5, "invalid values do not corrupt battery");
battery.Toggle(false);
Check(!battery.Active, "active amplifier can always be switched off");
Console.WriteLine("Portable megaphone battery checks passed.");

await DirectVoiceTests.Run();

var hostFeed = new HostVoiceFeed();
long hostTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
long hostConsumed = -1;
hostFeed.Store(7, .4, true, hostTime);
Check(!hostFeed.TryRead(8, ref hostConsumed, out _, out _, hostTime), "relay is isolated by player");
Check(hostFeed.TryRead(7, ref hostConsumed, out var hostRms, out var hostActive, hostTime) && hostRms == .4 && hostActive, "host sample delivered");
Check(!hostFeed.TryRead(7, ref hostConsumed, out _, out _, hostTime), "host sample consumed once");
hostFeed.Store(7, .4, false, hostTime);
Check(hostFeed.TryRead(7, ref hostConsumed, out _, out hostActive, hostTime) && !hostActive, "muted mobile propagated");
long staleConsumed = -1;
Check(!hostFeed.TryRead(7, ref staleConsumed, out _, out _, hostTime + 501), "host disconnection expires");
Check(!HostVoiceFeed.Parse("TBCLH1|wrong|7|1|10000|0.4|1", "secret", 10000, out _, out _, out _, out _), "host token mismatch");
Check(!HostVoiceFeed.Parse("TBCLH1|secret|7|1|9000|0.4|1", "secret", 10000, out _, out _, out _, out _), "stale host packet");
Check(!HostVoiceFeed.Parse("TBCLH1|secret|7|1|10000|NaN|1", "secret", 10000, out _, out _, out _, out _), "host NaN rejected");
Check(HostVoiceFeed.Parse("TBCLH1|secret|7|1|10000|0.4|0", "secret", 10000, out var hostPlayer, out _, out _, out hostActive) && hostPlayer == 7 && !hostActive, "host packet parsed");
using (var hostReader = new SharedVoiceBridge(251, Path.Combine(Path.GetTempPath(), "tanuki-host-feed-test"), 0))
{
 HostVoiceRelay.Feed.Store(251, .65, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
 Check(hostReader.TryReadSample(out _, out hostRms, out hostActive) && hostRms == .65 && hostActive && hostReader.Connected, "host feed reaches actual role bridge");
}
Console.WriteLine("PASS host relay validation and role bridge");
