using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Nebula;
using Nebula.Map;
using Nebula.Player;
using Nebula.Utilities;
using UnityEngine;
using Virial;
using Virial.Game;

namespace TanukiBCL.Roles;

// The only clone-specific dependency on NoS internal spawn/walk implementation.
// Do not copy the fake-player netcode or map navigation data into this addon.
internal sealed class CloneRuntime : IDisposable
{
    internal static readonly Virial.Media.Image Icon = SpriteLoader.FromResource("Nebula.Resources.Buttons.DoppelgangerSwapButton.png", 115f);
    private readonly IFakePlayer fake;
    private readonly System.Random random = new();
    private Coroutine? walk;
    private bool disposed;
    public int Id => ((IPlayerlike)fake).PlayerlikeId;
    public bool Active => !disposed && fake.IsActive;
    public CloneRuntime(Virial.Game.Player owner, ILifespan lifespan)
    {
        var player = owner;
        if (MapData.GetCurrentMapData().MapNavData?.MainNodes.Count is not > 0)
            throw new InvalidOperationException("Clone navigation is unavailable on this map.");
        fake = FakePlayerController.SpawnSyncFakePlayer(player,
            new(player.Position, KillCharacteristics.Disappear, true, true, player.VanillaCosmetics.FlipX, null))
            .BindLifespan(lifespan);
        try { walk = NebulaManager.Instance.StartCoroutine(Wander().WrapToIl2Cpp()); }
        catch { fake.Release(); throw; }
    }
    private NavPath? Destination()
    {
        var nodes = MapData.GetCurrentMapData().MapNavData.MainNodes;
        if (nodes.Count == 0) return null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var node = nodes[random.Next(nodes.Count)];
            var target = new Vector2(node.X, node.Y);
            if (Vector2.Distance(fake.TruePosition, target) < 2f) continue;
            var path = NavVerticesHelpers.CalcPath(fake.TruePosition, target);
            if (path != null && path.Path.Length > 1) return path;
        }
        return null;
    }
    private IEnumerator Wander()
    {
        while (Active)
        {
            var path = Destination();
            double until = Time.unscaledTimeAsDouble + 30;
            if (path != null)
                yield return NavVerticesHelpers.WalkPath(path.Path, path.StopCond, ((FakePlayerController)fake).Logics,
                    () => disposed || !fake.IsActive || Time.unscaledTimeAsDouble >= until);
            yield return Effects.Wait(.75f);
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (walk != null && NebulaManager.Instance != null) NebulaManager.Instance.StopCoroutine(walk);
        walk = null; fake.Release();
    }
}
