using System;
using System.Linq.Expressions;
using System.Reflection;

namespace TanukiBCL.Roles;

// Uses only the new event's public API, discovered once. The addon still loads
// on NoS builds without TBCLEvent; no hidden-member access is required.
public sealed class TanukiAddonDataApi
{
    public Type EventType { get; }
    public Action<object, byte, int> UpdatePlayer { get; }
    public Action<object, int> UpdateSnapshot { get; }
    private TanukiAddonDataApi(Type type, Action<object, byte, int> player, Action<object, int> snapshot)
    { EventType = type; UpdatePlayer = player; UpdateSnapshot = snapshot; }
    public static TanukiAddonDataApi? TryCreate(Type type)
    {
        if (!type.IsVisible) return null;
        var player = type.GetMethod("UpdatePlayerData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(byte), typeof(int) }, null);
        var snapshot = type.GetMethod("UpdateSnapshotData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
        if (player?.ReturnType != typeof(void) || snapshot?.ReturnType != typeof(void)) return null;
        var target = Expression.Parameter(typeof(object));
        var id = Expression.Parameter(typeof(byte));
        var data = Expression.Parameter(typeof(int));
        var updatePlayer = Expression.Lambda<Action<object, byte, int>>(
            Expression.Call(Expression.Convert(target, type), player, id, data), target, id, data).Compile();
        var updateSnapshot = Expression.Lambda<Action<object, int>>(
            Expression.Call(Expression.Convert(target, type), snapshot, data), target, data).Compile();
        return new TanukiAddonDataApi(type, updatePlayer, updateSnapshot);
    }
}
