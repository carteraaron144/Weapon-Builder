namespace WeaponBuilder.Core;

/// Rules for whether and how a part fits a socket.
public static class Fitting
{
    /// A socket accepts a part when they share at least one tag.
    public static bool Accepts(SocketDef socket, PartDef part) =>
        socket.Accepts.Any(tag => part.Tags.Contains(tag));
}
