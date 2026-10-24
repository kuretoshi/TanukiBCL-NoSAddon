using Nebula.Roles.Crewmate;
using Nebula.Roles.Impostor;
using Virial;
using Virial.Assignable;
using Virial.Game;

namespace TanukiBCL.Roles;

public sealed class SuperHero : DefinedSingleAbilityRoleTemplate<HeroAbility>, DefinedRole, IAssignableDocument
{
    private static readonly HeroRoleSettings Options = new("tanukiSuperHero");
    public static readonly SuperHero MyRole = new();
    private SuperHero() : base("tanukiSuperHero", new Virial.Color(70, 180, 245), RoleCategory.CrewmateRole, Crewmate.MyTeam, Options.All)
    {
        HeroAbility.EnsureRpcRegistered();
        ConfigurationHolder!.Illustration = HeroVisual.SuperIcon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => HeroVisual.SuperIcon;
    bool IAssignableDocument.HasTips => true;
    public override HeroAbility CreateAbility(Player player, int[] arguments) => new(player, HeroKind.Super, Options);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToCrewmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;
}

public sealed class EvilHero : DefinedSingleAbilityRoleTemplate<HeroAbility>, DefinedRole, IAssignableDocument
{
    private static readonly HeroRoleSettings Options = new("tanukiEvilHero");
    public static readonly EvilHero MyRole = new();
    private EvilHero() : base("tanukiEvilHero", Virial.Color.ImpostorColor, RoleCategory.ImpostorRole, Impostor.MyTeam, Options.All)
    {
        HeroAbility.EnsureRpcRegistered();
        ConfigurationHolder!.Illustration = HeroVisual.EvilIcon;
        ConfigurationHolder.AddTags(AddonRoleTag.Tag);
    }
    Virial.Media.Image? DefinedAssignable.IconImage => HeroVisual.EvilIcon;
    bool IAssignableDocument.HasTips => true;
    public override HeroAbility CreateAbility(Player player, int[] arguments) => new(player, HeroKind.Evil, Options);
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.Killers;
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;
}
