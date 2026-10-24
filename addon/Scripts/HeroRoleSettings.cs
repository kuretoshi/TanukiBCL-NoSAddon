using Virial;
using Virial.Configuration;

namespace TanukiBCL.Roles;

public sealed class HeroRoleSettings
{
    public readonly FloatConfiguration Cooldown, Seconds, KillCooldown, WaitSeconds;
    public readonly BoolConfiguration HideName;
    public HeroRoleSettings(string role)
    {
        string key = "options.role." + role + ".";
        Cooldown = NebulaAPI.Configurations.Configuration(key + "cooldown", (0f, 120f, 5f), 30f, FloatConfigurationDecorator.Second);
        Seconds = NebulaAPI.Configurations.Configuration(key + "seconds", (1f, 120f, 1f), 10f, FloatConfigurationDecorator.Second);
        KillCooldown = NebulaAPI.Configurations.Configuration(key + "killCooldown", (0f, 30f, 2.5f), 2.5f, FloatConfigurationDecorator.Second);
        WaitSeconds = NebulaAPI.Configurations.Configuration(key + "waitSeconds", (1f, 15f, 1f), 5f, FloatConfigurationDecorator.Second);
        HideName = NebulaAPI.Configurations.Configuration(key + "hideName", false);
    }
    public IConfiguration[] All => new IConfiguration[] { Cooldown, Seconds, KillCooldown, WaitSeconds, HideName };
}
