using UnityEngine;
using Virial.Media;

namespace TanukiBCL.Roles;

// Shared artwork for this role's list entry, buttons and existing sprite consumers.
public static class DyingVoiceVisual
{
    public static readonly Image Icon = VoiceRoleIcons.DyingVoiceMessage;
    public static Sprite GetSprite() => VoiceRoleIcons.DyingVoiceSprite;
}
