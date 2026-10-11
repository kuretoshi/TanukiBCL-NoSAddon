using System;

namespace TanukiBCL.Roles;

public sealed class MegaphoneBattery
{
    public double Charge { get; private set; }
    public bool Active { get; private set; }
    public MegaphoneBattery(double charge = 1) => Charge = Math.Clamp(charge, 0, 1);
    public bool Toggle(bool eligible)
    {
        if (Active) { Active = false; return true; }
        if (!eligible || Charge <= 0) return false;
        Active = true;
        return true;
    }
    public void Stop() => Active = false;
    public void Update(double seconds, double capacitySeconds, bool eligible)
    {
        if (!eligible) { Stop(); return; }
        if (!Active || !double.IsFinite(seconds) || seconds <= 0 || !double.IsFinite(capacitySeconds) || capacitySeconds <= 0) return;
        Charge = Math.Max(0, Charge - seconds / capacitySeconds);
        if (Charge <= 0) Stop();
    }
    public void Recharge(double percentage)
    {
        if (double.IsFinite(percentage) && percentage > 0) Charge = Math.Min(1, Charge + percentage / 100);
    }
}
