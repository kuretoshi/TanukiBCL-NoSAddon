using System;
using System.IO;
using System.Threading.Tasks;
using Nebula;
using Virial;
using Virial.Configuration;
using Virial.Media;
using Virial.Text;

namespace TanukiBCL.Roles;

public static class NGWordSettings
{
    public static readonly NGWordFiles Files = new(Path.Combine(Environment.CurrentDirectory, "TanukiBCL"), Template);
    private static bool busy;
    private static string message = "";
    private static string T(string key) => NebulaAPI.Language.Translate("tanuki.ngFiles." + key);
    // NoS also exposes role settings in freeplay, where no LobbyBehaviour exists.
    private static bool CanEdit => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    private static string Template()
    {
        using var source = NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("NGWords.template.txt")?.AsStream()
            ?? throw new IOException("NG word template is missing.");
        using var reader = new StreamReader(source, System.Text.Encoding.UTF8, true);
        return reader.ReadToEnd();
    }
    public static IConfiguration Option() => NebulaAPI.Configurations.Configuration(() => null, Widget, () => CanEdit);
    private static GUIWidget Widget()
    {
        Refresh();
        var attribute = new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsTitleHalf))
            { Size = new(4.7f, .32f), Wrapping = false, Alignment = TextAlignment.Center, FontSize = new(1.6f, 1.4f, 1.6f) };
        return NebulaAPI.GUI.VerticalHolder(GUIAlignment.Center,
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center, attribute, "tanuki.ngInput.preview", _ => NGWordSubmission.ShowPreview()),
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center, attribute, "tanuki.ngFiles.browse", _ => Begin(0)),
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center, attribute, "tanuki.ngFiles.template", _ => Begin(1)),
            NebulaAPI.GUI.LocalizedButton(GUIAlignment.Center, attribute, "tanuki.ngFiles.current", _ => Begin(2)),
            NebulaAPI.GUI.RealtimeText(GUIAlignment.Center,
                new TextAttribute(NebulaAPI.GUI.GetAttribute(AttributeAsset.OptionsValue))
                    { Size = new(6f, .9f), Wrapping = true, Alignment = TextAlignment.Center, FontSize = new(1.4f, 1.2f, 1.4f) },
                () => busy ? T("busy") : message, 55));
    }
    private static void Refresh(string? result = null)
    {
        try
        {
            int count = Files.Load().Length;
            string name = Path.GetFileName(Files.SourcePath).Replace("<", "＜").Replace(">", "＞");
            if (name.Length > 48) name = name.Substring(0, 45) + "…";
            message = T("selected") + " " + name + " (" + count + T("words") + ")\n" + (result ?? T("hint"));
        }
        catch (Exception ex) when (IsFileError(ex)) { message = T("failed") + "\n" + T("hint"); }
    }
    private static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException;
    private static void Begin(int action)
    {
        if (busy || !CanEdit) return;
        try
        {
            string initial = action == 1 ? Path.Combine(Path.GetDirectoryName(Files.SourcePath)!, "NGWords.template.txt") : Files.SourcePath;
            if (action == 2) Files.CurrentText();
            var task = NGWordFileDialog.Pick(action != 0, T(action == 0 ? "browse" : action == 1 ? "template" : "current"),
                initial, T("filterText") + "\0*.txt\0" + T("filterAll") + "\0*.*\0\0");
            busy = true;
            Watch(task, action);
        }
        catch (Exception ex) { Failed(ex); }
    }
    private static void Watch(Task<string?> task, int action)
    {
        NebulaManager.Instance.StartDelayAction(.1f, () =>
        {
            if (!task.IsCompleted) { Watch(task, action); return; }
            busy = false;
            if (task.IsCanceled || !CanEdit) return;
            if (task.IsFaulted) { Failed(task.Exception!.GetBaseException()); return; }
            if (task.Result == null) { Refresh(); return; }
            Apply(task.Result, action);
        });
    }
    private static void Apply(string path, int action)
    {
        try
        {
            if (action == 0) Files.Select(path);
            else if (action == 1) Files.SaveTemplate(path);
            else Files.SaveCurrent(path);
            Refresh(T(action == 0 ? "loaded" : "saved"));
        }
        catch (Exception ex) when (IsFileError(ex)) { Failed(ex); }
    }
    private static void Failed(Exception ex)
    {
        busy = false;
        Refresh(T("failed"));
        NebulaAPI.Logging.NebulaLogger("TanukiNGWords").Error(ex.Message);
    }
}
