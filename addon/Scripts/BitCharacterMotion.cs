using System;

namespace TanukiBCL.Roles;

public static class BitCharacterMotion
{
    private static double Phase(float time) => time - Math.Floor(time);

    public static int WalkFrame(float normalizedTime, int bits)
    {
        if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime)) return 0;
        double phase = Phase(normalizedTime);
        int frame = Math.Min(11, (int)(phase * 12 + .000001));
        // Native 12-frame Walk: left stride, left contact, right stride,
        // right contact. Keep original pivots and the baked ground shadow.
        return bits is 2 or 4 or 8 ? frame < 4 ? 0 : frame < 6 ? 4 : frame < 10 ? 6 : 10 : frame;
    }

    public static long WalkTick(float normalizedTime)
    {
        if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime)) return long.MinValue;
        int frame = WalkFrame(normalizedTime, 2);
        int step = frame == 0 ? 0 : frame == 4 ? 1 : frame == 6 ? 2 : 3;
        return (long)Math.Floor(normalizedTime) * 4 + step;
    }
}
