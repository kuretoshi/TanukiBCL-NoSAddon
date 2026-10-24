using System;
using Virial;
using Virial.Components;

namespace TanukiBCL.Roles;

public sealed class HeroTransformTimer : SimpleLifespan, IVisualTimer
{
    private readonly Func<double> remaining, duration;
    private readonly Func<bool> progressing;
    public HeroTransformTimer(Func<double> remaining, Func<double> duration, Func<bool> progressing)
    {
        this.remaining = remaining; this.duration = duration; this.progressing = progressing;
    }
    public float Percentage => (float)Math.Clamp(remaining() / Math.Max(1, duration()), 0, 1);
    public float CurrentTime => (float)remaining();
    public string TimerText => Math.Ceiling(remaining()).ToString("0") + "s";
    public bool IsProgressing => progressing();
    public IVisualTimer Start(float? time = null) => this;
}
