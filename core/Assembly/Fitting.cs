namespace WeaponBuilder.Core;

/// Rules for whether and how a part fits a socket.
public static class Fitting
{
    /// A socket accepts a part when they share at least one tag.
    public static bool Accepts(SocketDef socket, PartDef part) =>
        socket.Accepts.Any(tag => part.Tags.Contains(tag));

    public const float MinScale = 0.25f;
    public const float MaxScale = 4f;

    /// The uniform scale that fits a part into a socket's size budget: the tighter of the
    /// length and width ratios, clamped so a huge part never vanishes or a tiny one explodes.
    public static float Scale(SocketDef socket, PartDef part) =>
        Math.Clamp(Math.Min(socket.Length / part.Length, socket.Width / part.Width), MinScale, MaxScale);

    /// A spin about a socket's axis, snapped to the socket's step: the nearest multiple of step in [0, 360),
    /// or 0 when the socket doesn't allow spinning (step 0 or less).
    public static float SnapRotation(float degrees, float stepDegrees)
    {
        if (stepDegrees <= 0f)
            return 0f;
        var snapped = MathF.Round(degrees / stepDegrees, MidpointRounding.AwayFromZero) * stepDegrees;
        return ((snapped % 360f) + 360f) % 360f;
    }
}
