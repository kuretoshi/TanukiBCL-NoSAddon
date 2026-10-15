using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TanukiBCL.Roles;

public static class InquiryReport
{
    public static string Redact(string text)
    {
        text = Regex.Replace(text, @"(?i)(https?://(?:discord(?:app)?\.com)/api/webhooks/)\S+", "$1[REDACTED]");
        text = Regex.Replace(text, @"(?i)(Bearer\s+)[A-Za-z0-9_.-]+", "$1[REDACTED]");
        text = Regex.Replace(text, @"(?i)((?:token|secret|password|authorization)\s*[=:]\s*)[^\s,;]+", "$1[REDACTED]");
        text = Regex.Replace(text, @"(?i)[A-Z]:[\\/]Users[\\/][^\\/\r\n]+", @"C:\Users\[USER]");
        return Regex.Replace(text, @"\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+)\b", "[REDACTED]");
    }

    public static (string name, string text)[] Collect(string gameDirectory)
    {
        string[] paths = { "NebulaLog.txt", Path.Combine("BepInEx", "LogOutput.log") };
        var logs = new (string name, string text)[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            string text;
            try
            {
                using var stream = new FileStream(Path.Combine(gameDirectory, paths[i]), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                const int limit = 256 * 1024;
                bool truncated = stream.Length > limit;
                if (truncated) stream.Seek(-limit, SeekOrigin.End);
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                text = reader.ReadToEnd();
                if (truncated) text = "[Earlier log omitted]\n" + text.Substring(Math.Max(0, text.IndexOf('\n') + 1));
                text = Redact(text);
            }
            catch (IOException) { text = "[Log unavailable]"; }
            catch (UnauthorizedAccessException) { text = "[Log unavailable]"; }
            logs[i] = (Path.GetFileName(paths[i]) + ".txt", text);
        }
        return logs;
    }

    public static async Task<string> Send(string subject, string body, (string name, string text)[] logs)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TanukiBCL-NoSAddon/0.1");
        using var form = new MultipartFormDataContent();
        var payload = new { product = "addon", tag = "bug", subject, body, public_issue_consent = true };
        form.Add(new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8), "payload_json");
        for (int i = 0; i < logs.Length; i++) form.Add(new StringContent(logs[i].text, Encoding.UTF8), "files[" + i + "]", logs[i].name);
        using var response = await client.PostAsync("https://debug-auth.kuretoshi.work/v1/inquiries", form);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("HTTP " + (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string url = json.RootElement.GetProperty("issue_url").GetString() ?? "";
        if (!Regex.IsMatch(url, @"\Ahttps://github\.com/kuretoshi/TanukiBCL-NoSAddon/issues/[0-9]+\z")) throw new InvalidOperationException("Invalid response");
        return url;
    }
}
