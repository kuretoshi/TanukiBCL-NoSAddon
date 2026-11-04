using System;
using System.Linq;
using System.Reflection;
using Nebula.Game;
using Virial;
using Virial.Events;

namespace TanukiBCL.Roles;

public static class TanukiAddonDataBridge
{
    private static TanukiAddonDataApi? api;
    private static bool discovered;
    private static object? boundGame;
    public static void EnsureGameHook()
    {
        var game = NebulaAPI.CurrentGame;
        if (game == null) { boundGame = null; return; }
        if (ReferenceEquals(boundGame, game)) return;
        boundGame = game;
        try
        {
            if (!discovered) { discovered = true; api = Discover(); }
            if (api == null) return;
            typeof(TanukiAddonDataBridge).GetMethod(nameof(Bind), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(api.EventType).Invoke(null, new object[] { game });
        }
        catch (Exception ex) { api = null; Log("AddonData unavailable; keeping legacy bridges: " + ex.Message); }
    }
    private static TanukiAddonDataApi? Discover()
    {
        var types = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name is "NebulaAPI" or "Nebula")
            .SelectMany(a => a.GetExportedTypes()).Where(t => t.Name == "TBCLEvent" && typeof(Event).IsAssignableFrom(t)).ToArray();
        var result = types.Length == 1 ? TanukiAddonDataApi.TryCreate(types[0]) : null;
        Log(result == null ? "TBCLEvent not available; using legacy bridges." : "TBCLEvent AddonData bridge enabled.");
        return result;
    }
    private static void Bind<TEvent>(ILifespan lifespan) =>
        GameOperatorManager.Instance?.Subscribe<TEvent>(ev => Publish(ev!), lifespan);
    private static void Publish(object ev)
    {
        if (api == null || NebulaAPI.CurrentGame == null || AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started) return;
        try
        {
            bool meeting = HeroSpeechHost.IsPresent(MeetingHud.Instance);
            var state = AddonLinkState.Snapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            foreach (var player in NebulaAPI.CurrentGame.GetAllPlayers())
            {
                if (player.PlayerId >= 24) continue;
                bool alive = !player.IsDead && !player.IsDisconnected;
                int hero = alive && !meeting ? HeroTransformBroadcast.AddonKindFor(player.PlayerId) : 0;
                float? range = alive && !meeting ? PortableMegaphoneBroadcast.AddonRangeFor(player.PlayerId) : null;
                int data = AddonLinkRuntime.Port > 0 ? TanukiAddonDataCodec.EncodeV2(hero, range, AddonLinkState.FlagsFor(player.PlayerId, state)) :
                    TanukiAddonDataCodec.Encode(hero, range);
                api.UpdatePlayer(ev, player.PlayerId, data);
            }
            api.UpdateSnapshot(ev, AddonLinkRuntime.Port > 0 ? TanukiAddonDataCodec.Endpoint(AddonLinkRuntime.Port) : TanukiAddonDataCodec.SnapshotMarker);
        }
        catch (Exception ex) { api = null; Log("AddonData publication disabled: " + ex.Message); }
    }
    private static void Log(string message) => NebulaAPI.Logging.NebulaLogger("TanukiAddonData").Message(message);
}
