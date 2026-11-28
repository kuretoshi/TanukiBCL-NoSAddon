using System;
using System.IO;
using System.Threading.Tasks;
using Nebula;
using Virial;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;

namespace TanukiBCL.Roles;

public static class VoiceAuthenticationWordSettings
{
    public static readonly NGWordFiles Files = new(Path.Combine(Environment.CurrentDirectory, "TanukiBCL"), Template, "VoiceAuthenticationWords");
    private static bool busy;
    private static string message = "";
    private static bool CanEdit => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    private static string T(string key) => VoiceAuthenticationTask.Text("files." + key);
    private static string Template()
    {
        using var stream = NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("VoiceAuthenticationWords.template.txt")?.AsStream()
            ?? throw new IOException("Voice authentication word template is missing.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true);
        return reader.ReadToEnd();
    }
    public static NGWordEntry[] LoadForGame()
    {
        try { return Files.Load(); }
        catch (Exception ex) when (IsFileError(ex))
        {
            NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Error("Word file could not be loaded; using built-in words. " + ex.Message);
            return VoiceAuthenticationWords.EntriesToWords();
        }
    }
    public static IConfiguration Option() => NebulaAPI.Configurations.Configuration(() => null, Widget, () => CanEdit);
    private static GUIWidget Widget()
    {
        Refresh();
        var attr = new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsTitleHalf))
        { Size = new(4.7f,.32f), Wrapping = false, Alignment = TextAlignment.Center, FontSize = new(1.6f,1.4f,1.6f) };
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center,attr,"tanuki.voiceAuthentication.files.browse",_ => Begin(0)),
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center,attr,"tanuki.voiceAuthentication.files.template",_ => Begin(1)),
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center,attr,"tanuki.voiceAuthentication.files.current",_ => Begin(2)),
            NebulaAPI.GUI.RealtimeText(GUIAlignment.Center,
                new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsValue))
                { Size = new(6f,1.1f), Wrapping = true, Alignment = TextAlignment.Center, FontSize = new(1.4f,1.2f,1.4f) },
                () => busy ? T("busy") : message,55));
    }
    private static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException;
    private static void Refresh(string? result = null)
    {
        try
        {
            int count = Files.Load().Length;
            string name = Path.GetFileName(Files.SourcePath).Replace("<","＜").Replace(">","＞");
            if (name.Length > 48) name = name.Substring(0,45) + "…";
            message = T("selected") + " " + name + " (" + count + T("words") + ")\n" + (result ?? T("hint"));
        }
        catch (Exception ex) when (IsFileError(ex)) { message = T("failed") + "\n" + T("fallback"); }
    }
    private static void Begin(int action)
    {
        if (busy || !CanEdit) return;
        try
        {
            string initial = action == 1 ? Path.Combine(Path.GetDirectoryName(Files.SourcePath)!,"VoiceAuthenticationWords.template.txt") : Files.SourcePath;
            if (action == 2) Files.CurrentText();
            var task = NGWordFileDialog.Pick(action != 0,T(action == 0 ? "browse" : action == 1 ? "template" : "current"),
                initial,"Text (*.txt)\0*.txt\0All files (*.*)\0*.*\0\0");
            busy = true;
            Watch(task,action);
        }
        catch (Exception ex) { Failed(ex); }
    }
    private static void Watch(Task<string?> task, int action)
    {
        NebulaManager.Instance.StartDelayAction(.1f,() =>
        {
            if (!task.IsCompleted) { Watch(task,action); return; }
            busy = false;
            if (task.IsCanceled || !CanEdit) return;
            if (task.IsFaulted) { Failed(task.Exception!.GetBaseException()); return; }
            if (task.Result == null) { Refresh(); return; }
            try
            {
                if (action == 0) Files.Select(task.Result);
                else if (action == 1) Files.SaveTemplate(task.Result);
                else Files.SaveCurrent(task.Result);
                Refresh(T(action == 0 ? "loaded" : "saved"));
            }
            catch (Exception ex) when (IsFileError(ex)) { Failed(ex); }
        });
    }
    private static void Failed(Exception ex)
    {
        busy = false;
        Refresh(T("failed"));
        NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Error(ex.Message);
    }
}
