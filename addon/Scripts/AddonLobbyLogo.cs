using HarmonyLib;
using Nebula;
using Nebula.Utilities;
using UnityEngine;
using Virial.Attributes;
using Virial.Runtime;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class AddonLobbyLogo
{
    private static SpriteRenderer? logo;

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.lobbyLogo").Patch(
            AccessTools.Method(typeof(HudManager), nameof(HudManager.Update)),
            postfix: new HarmonyMethod(typeof(AddonLobbyLogo), nameof(Update)));
    }

    public static void Update()
    {
        if (LobbyBehaviour.Instance == null || AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Joined)
        {
            if (logo) Object.Destroy(logo!.gameObject);
            logo = null;
            return;
        }
        var hud = HudManager.Instance;
        if (logo || hud == null) return;
        var obj = new GameObject("TanukiLobbyLogo");
        obj.layer = hud.gameObject.layer;
        obj.transform.SetParent(hud.transform, false);
        logo = obj.AddComponent<SpriteRenderer>();
        logo.sprite = AddonTitleLogo.GetSprite();
        logo.color = new Color(1f, 1f, 1f, .75f);
        obj.SetAsUIAspectContent(AspectPosition.EdgeAlignments.LeftTop, new Vector3(.975f, 2f, -6f));
        obj.GetComponent<AspectPosition>().updateAlways = true;
        Align(hud, logo);
        NebulaManager.Instance.StartDelayAction(.5f, () => Align(hud, logo));
        NebulaManager.Instance.StartDelayAction(2f, () => Align(hud, logo));
    }

    private static void Align(HudManager hud, SpriteRenderer? image)
    {
        if (!hud || !image) return;
        var reference = hud.transform.Find("UchuAddonLogoHolder/UchuAddonLogo")?.GetComponent<SpriteRenderer>()
            ?? hud.transform.Find("NebulaLogoHolder/NebulaLogo")?.GetComponent<SpriteRenderer>();
        if (reference == null) return;
        var aspect = image!.GetComponent<AspectPosition>();
        var offset = aspect.DistanceFromEdge;
        offset.x += reference.bounds.center.x - image.bounds.center.x;
        offset.y += image.bounds.max.y - (reference.bounds.min.y - .06f);
        aspect.DistanceFromEdge = offset;
        aspect.AdjustPosition();
    }
}
