using System;

namespace TanukiBCL.Roles;

// Versioned values in NoS's shared int32 addon fields. Unknown/reserved bits
// must not be interpreted as Tanuki state by a receiving client.
public static class TanukiAddonDataCodec
{
    public const int SnapshotMarker = 0x544B0001;
    public const int PlayerMarker = 0x544B0000;
    public const int EndpointMarker = 0x54420000;
    public const int PlayerV2Marker = 0x544C0000;
    public static int Endpoint(int port) => port > 0 && port <= 65535 ? EndpointMarker | port : 0;
    public static int EncodeV2(int hero, float? portableRange, int flags) =>
        PlayerV2Marker | (Encode(hero, portableRange) & 0x1FF) | (flags & 0xFE00);
    public static int Encode(int hero, float? portableRange)
    {
        if (portableRange.HasValue && !float.IsFinite(portableRange.Value)) portableRange = null;
        int range = portableRange.HasValue ? (int)Math.Round(Math.Clamp(portableRange.Value, 0, 30) * 2) : 0;
        return PlayerMarker | Math.Clamp(hero, 0, 2) | (range << 2) | (portableRange.HasValue ? 0x100 : 0);
    }
    public static bool TryDecode(int data, out int hero, out float? portableRange)
    {
        hero = 0; portableRange = null;
        if ((data & unchecked((int)0xFFFF0000)) == PlayerV2Marker) data = PlayerMarker | (data & 0x1FF);
        if ((data & unchecked((int)0xFFFFFE00)) != PlayerMarker) return false;
        int kind = data & 3, range = (data >> 2) & 63;
        bool active = (data & 0x100) != 0;
        if (kind == 3 || range > 60 || (!active && range != 0)) return false;
        hero = kind;
        if (active) portableRange = range / 2f;
        return true;
    }
}
