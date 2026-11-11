using TanukiBCL.Roles;

internal static class WallEarTests
{
    internal static void Run(Action<bool, string> check)
    {
        var state = new WallEarRound();
        check(state.Place(1, 0, 0, 0, 2, 15), "ear: initial placement");
        check(!state.Place(2, 0, 1, 14.99, 2, 15), "ear: cooldown enforced outside button");
        check(!state.Place(1, 0, 0, 15, 2, 15), "ear: duplicate placement rejected");
        check(!state.Listen(1, 1, 5, 5), "ear: cannot listen remotely");
        check(state.Listen(1, 1, 1, 0) && state.Active?.Id == 1, "ear: nearby listener activates");
        check(!state.Place(2, 0, 1, 15, 2, 15), "ear: cannot place while listening");
        check(state.Listen(2, -1, 0, 0) && state.Active == null, "ear: second click stops");
        check(!state.Listen(1, 1, 0, 0), "ear: late activation cannot undo stop");
        check(state.Place(2, 0, 1, 15, 2, 15), "ear: cooldown boundary allows placement");
        check(!state.Place(3, 0, 2, 30, 2, 15), "ear: placement limit enforced");
        check(state.Listen(3, 2, 0, 1), "ear: can select a different installed ear");
        state.Stop();
        check(state.Active == null && state.Ears.Count == 2, "ear: meeting stops listening and keeps ears");
        state.Clear();
        check(state.Active == null && state.Ears.Count == 0, "ear: role removal clears ears");
        check(!state.Place(3, float.NaN, 0, 30, 2, 15), "ear: invalid position rejected");
        check(!state.Place(3, 0, 0, double.NaN, 2, 15), "ear: invalid clock rejected");
        check(!state.Place(3, 0, 0, 30, 2, float.PositiveInfinity), "ear: invalid cooldown rejected");
        check(!WallEarRound.Nearby(new(1, 0, 0), float.NaN, 0), "ear: invalid player position rejected");
        Console.WriteLine("PASS wall ear: placement, limits, interaction distance, replay, meeting and cleanup");
    }
}
