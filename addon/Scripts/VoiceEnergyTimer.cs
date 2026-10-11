using System;
using Virial;
using Virial.Components;

namespace TanukiBCL.Roles;

public sealed class VoiceEnergyTimer : SimpleLifespan, IVisualTimer
{
    private readonly Func<float> energy;
    private readonly Func<bool> charging;
    public VoiceEnergyTimer(Func<float> energy, Func<bool> charging) { this.energy = energy; this.charging = charging; }
    public float Percentage => energy();
    public float CurrentTime => energy();
    public string TimerText => "<size=75%><nobr>" + Math.Round(Math.Clamp(energy(), 0f, 1f) * 100).ToString() + "%</nobr></size>";
    public bool IsProgressing => charging();
    public IVisualTimer Start(float? time = null) => this;
}
