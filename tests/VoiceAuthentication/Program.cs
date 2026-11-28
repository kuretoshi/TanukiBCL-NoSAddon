using TanukiBCL.Roles;

void Check(bool value, string name) { if (!value) throw new Exception(name); }
for (int count = 1; count <= 5; count++)
{
    var words = VoiceAuthenticationWords.Select(count, new Random(42));
    Check(words.Length == count && words.Distinct().Count() == count && words.All(VoiceAuthenticationWords.Valid), "Distinct bounded task words");
    Check(words.SequenceEqual(VoiceAuthenticationWords.Select(count, new Random(42))), "Reproducible selection");
}
foreach (var entry in VoiceAuthenticationWords.Entries)
    Check(entry.Length is >= 1 and <= 3 && entry.All(w => w.Length is >= 2 and <= 32), "Keywords meet all existing desktop bridge limits");
var selected = VoiceAuthenticationWords.Select(3, new Random(3));
var progress = new VoiceAuthenticationProgress(selected);
Check(!progress.Accept(1) && progress.Completed == 0, "Recognition without an open task cannot advance");
Check(progress.Begin(1000000001), "Begin first word");
Check(!progress.Begin(1000000002), "Only one pending word");
Check(!progress.Accept(1000000002) && progress.Completed == 0, "Reject another request");
Check(progress.Accept(1000000001) && progress.Completed == 1, "One recognized word advances one step");
Check(!progress.Accept(1000000001), "Duplicate result cannot advance twice");
Check(progress.Begin(1000000002), "Next word has a distinct request");
progress.Pause();
Check(progress.Completed == 1 && progress.Entries.SequenceEqual(selected), "Closing preserves all selected words and progress");
Check(!progress.Accept(1000000002) && !progress.Begin(1000000002), "Close blocks stale recognition and reused requests");
Check(progress.Begin(1000000003) && progress.Accept(1000000003), "Reopen and complete the second word");
Check(progress.Begin(1000000004) && progress.Accept(1000000004) && progress.Finished, "All required words complete the task");
Check(!progress.Begin(1000000005) && !progress.Accept(1000000004), "Completion is final");
var restored = new VoiceAuthenticationProgress(selected, 1);
Check(restored.CurrentEntry == selected[1] && restored.Completed == 1, "Normal task step can restore progress");
foreach (var invalid in new[] { Array.Empty<int>(), new[] { 0, 0 }, new[] { -1 }, new[] { 999 } })
{
    bool rejected = false;
    try { _ = new VoiceAuthenticationProgress(invalid); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "Invalid task definition rejected");
}
Console.WriteLine("PASS voice authentication: word catalog, selection, requests, resume, stale/replay rejection and completion");

var assembly = System.Reflection.Assembly.GetExecutingAssembly();
using var templateStream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("VoiceAuthenticationWords.template.txt")))!;
using var templateReader = new StreamReader(templateStream);
string template = templateReader.ReadToEnd();
var defaults = NGWordList.Parse(template);
Check(defaults.Length == 12 && defaults.Select(w => w.Display).SequenceEqual(VoiceAuthenticationWords.Entries.Select(w => w[0])), "Shipped file template matches default catalog");
var custom = NGWordList.Parse("\uFEFF# custom UTF8\n\n蒸着|じょうちゃく|ジョウチャク\n宇宙船|うちゅうせん\n通信室|つうしんしつ\nカセット|かせっと\n");
var assignments = new VoiceAuthenticationWordAssignments(custom);
custom[0].Words[1] = "変更済み";
var picked = assignments.GetOrCreate(1, 10, 3, new Random(42));
Check(picked.Length == 3 && picked.Select(w => w.Display).Distinct().Count() == 3, "Host selects distinct custom words");
Check(ReferenceEquals(picked, assignments.GetOrCreate(1,10,5,new Random(900))), "Reopen, retries and changed settings preserve assigned words");
var all = assignments.GetOrCreate(2,20,5,new Random(42));
Check(all.Length == 4 && all.Single(w => w.Display == "蒸着").Words[1] == "じょうちゃく", "Game snapshot is isolated from caller edits and clamps count to available words");
var wire = VoiceAuthenticationWordAssignments.Encode(picked);
var received = VoiceAuthenticationWordAssignments.Decode(wire);
Check(received.Select(w => string.Join("|",w.Words)).SequenceEqual(picked.Select(w => string.Join("|",w.Words))), "Clients receive host-selected display words and every accepted reading without local files");
var customProgress = new VoiceAuthenticationProgress(received);
Check(customProgress.Begin(1000001001) && customProgress.Accept(1000001001), "Custom word advances through the existing recognition request protocol");
var customResume = new VoiceAuthenticationProgress(customProgress.Words, customProgress.Completed);
Check(customResume.Completed == 1 && customResume.Words.Select(w => w.Display).SequenceEqual(received.Select(w => w.Display)), "Custom words survive close/resume independently of built-in indexes");
foreach (string bad in new[] { "# empty", new string('x',636), VoiceAuthenticationWordAssignments.Encode(defaults.Take(6).ToArray()), "<bad>|bad" })
{
    bool rejected = false;
    try { VoiceAuthenticationWordAssignments.Decode(bad); } catch (ArgumentException) { rejected = true; }
    Check(rejected,"Reject invalid, oversized and excessive assignments");
}
string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(),"TanukiVoiceTask-"+Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(testRoot);
try
{
    var ng = new NGWordFiles(testRoot,() => "# NG list\n会議|かいぎ\n");
    var taskFiles = new NGWordFiles(testRoot,() => template,"VoiceAuthenticationWords");
    ng.EnsureDefault(); taskFiles.EnsureDefault();
    Check(taskFiles.Load().Length == 12 && ng.Load().Single().Display == "会議", "Task and NG defaults coexist independently");
    string selectedFile = Path.Combine(testRoot,"選択リスト.txt");
    string selectedText = "# retained comments\n蒸着|じょうちゃく|ジョウチャク\n宇宙船|うちゅうせん\n";
    File.WriteAllText(selectedFile,selectedText,new System.Text.UTF8Encoding(true));
    Check(taskFiles.Select(selectedFile) == 2,"Custom list accepts BOM and aliases");
    var reopened = new NGWordFiles(testRoot,() => template,"VoiceAuthenticationWords");
    Check(reopened.SourcePath == selectedFile && reopened.Load()[0].Words.Length == 3 && ng.SourcePath == ng.DefaultFile,"Restart preserves task selection without changing NG selection");
    string output = Path.Combine(testRoot,"出力.txt"), savedTemplate = Path.Combine(testRoot,"テンプレート.txt");
    reopened.SaveCurrent(output); reopened.SaveTemplate(savedTemplate);
    Check(File.ReadAllText(output) == selectedText && File.ReadAllText(savedTemplate) == template && reopened.SourcePath == selectedFile,"Exports preserve comments and readings and do not change selection");
    string invalid = Path.Combine(testRoot,"invalid.txt"); File.WriteAllText(invalid,"# empty");
    bool rejected=false; try { reopened.Select(invalid); } catch (ArgumentException) { rejected=true; }
    Check(rejected && reopened.SourcePath == selectedFile,"Invalid import retains the previous task list");
    var small = new VoiceAuthenticationWordAssignments(reopened.Load()).GetOrCreate(9,7,5,new Random(3));
    Check(small.Length == 2,"A two-word file produces a finishable two-step task even when configured for five");
    File.WriteAllText(selectedFile,"# corrupt");
    rejected=false; try { reopened.SaveCurrent(output); } catch (ArgumentException) { rejected=true; }
    Check(rejected && File.ReadAllText(output) == selectedText,"Corrupt file never destroys a previously exported list");
    File.Delete(selectedFile);
    rejected=false; try { reopened.Load(); } catch (IOException) { rejected=true; }
    Check(rejected && NGWordList.Parse(template).Length == 12,"Missing selected file reports a load error; built-in fallback remains valid");
    File.WriteAllText(invalid,new string('x',131073));
    rejected=false; try { reopened.Select(invalid); } catch (IOException) { rejected=true; }
    Check(rejected,"Reject files larger than 128 KiB before parsing");
}
finally
{
    if (Path.GetDirectoryName(testRoot) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(testRoot).StartsWith("TanukiVoiceTask-"))
        throw new InvalidOperationException("Unexpected test cleanup path");
    Directory.Delete(testRoot,true);
}
NGWordFileTests.Run(Check);
Console.WriteLine("PASS custom task files: host assignment, aliases, resume, bounded wire payloads, imports, exports, persistence, NG isolation and invalid/missing file handling");
