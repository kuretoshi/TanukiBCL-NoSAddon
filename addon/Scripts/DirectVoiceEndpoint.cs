using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Nebula;
using Virial;
using UnityEngine;

namespace TanukiBCL.Roles;

public static class DirectVoiceEndpoint
{
    public static DirectVoiceServer? Current { get; private set; }
    private static string error = "";
    private static string notice = "";
    private static string T(string key) => NebulaAPI.Language.Translate("tanuki.directVoice." + key);
    public static string Address { get; private set; } = "";
    public static void Toggle()
    {
        if (Current != null) { Stop(); return; }
        if (PlayerControl.LocalPlayer == null || LobbyBehaviour.Instance == null)
        {
            error = T("lobbyRequired");
            return;
        }
        error = "";
        notice = "";
        try
        {
            string directory = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TanukiBCL", "NoS");
            var server = new DirectVoiceServer(PlayerControl.LocalPlayer.PlayerId, directory);
            Current = server;
            var lan = Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
            Address = "wss://" + (lan?.ToString() ?? "localhost") + ":" + server.Port + "/voice#" + server.Token;
            Watch(server, PlayerControl.LocalPlayer);
        }
        catch (Exception ex) { Stop(); error = ex.Message; }
    }
    private static void Watch(DirectVoiceServer server, PlayerControl owner)
    {
        NebulaManager.Instance.StartDelayAction(.5f, () =>
        {
            if (Current != server) return;
            if (PlayerControl.LocalPlayer != owner || owner.PlayerId != server.PlayerId) Stop();
            else Watch(server, owner);
        });
    }
    public static void Copy()
    {
        if (Current == null) { error = T("startFirst"); return; }
        GUIUtility.systemCopyBuffer = Address;
        notice = T("copied");
    }
    public static void Stop() { Current?.Dispose(); Current = null; Address = ""; error = T("stopped"); notice = ""; }
    public static string Status()
    {
        if (Current == null) return error.Length > 0 ? error
            : PlayerControl.LocalPlayer == null || LobbyBehaviour.Instance == null ? T("lobbyRequired") : T("hint");
        return NebulaAPI.Language.Translate(Current.OwnsInput(Current.PlayerId) ? "tanuki.directVoice.connected" : "tanuki.directVoice.waiting")
            + "\n<size=65%><nobr>" + Address + "</nobr></size>"
            + (notice.Length > 0 ? "\n" + notice : "");
    }
}
