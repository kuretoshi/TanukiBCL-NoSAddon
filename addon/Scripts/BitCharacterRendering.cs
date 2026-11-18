using HarmonyLib;
using Nebula;
using Nebula.Modules.Cosmetics;
using Virial.Attributes;
using Virial.Runtime;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class BitCharacterRendering
{
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.bitRendering").Patch(
            AccessTools.Method(typeof(NebulaCosmeticsLayer), nameof(NebulaCosmeticsLayer.LateUpdate)),
            postfix: new HarmonyMethod(typeof(BitCharacterRendering), nameof(AfterUpdate)) { priority = Priority.Last });
    }

    // Run after NoS selects the sprite and sets sorting orders, regardless of
    // Unity's ordering of the game event and individual cosmetic components.
    public static void AfterUpdate(NebulaCosmeticsLayer __instance)
    {
        if (__instance.MyLayer != null) HeroCostume.AfterCosmeticsUpdate(__instance.MyLayer.GetInstanceID());
    }
}
