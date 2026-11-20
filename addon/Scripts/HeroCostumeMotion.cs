using System;

namespace TanukiBCL.Roles;

public static class HeroCostumeMotion
{
    public static int Frame(float normalizedTime, int length)
    {
        if (length <= 1 || float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime)) return 0;
        double phase = normalizedTime - Math.Floor(normalizedTime);
        return Math.Min(length - 1, (int)(phase * length + .000001));
    }
}
