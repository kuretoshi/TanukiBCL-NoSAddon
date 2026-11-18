using System;

namespace TanukiBCL.Roles;

public static class BitCharacterMotion
{
    // Match MoreCosmic's eight frames per native run cycle. Two small rises
    // keep the feet stepping without reintroducing the full hat-node bounce.
    public static float WalkOffset(float normalizedTime)
    {
        if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime)) return 0f;
        double phase = normalizedTime - Math.Floor(normalizedTime);
        int step = (int)(phase * 8) % 4;
        return step == 2 ? .07f : step == 0 ? 0f : .035f;
    }
}
