using System.Numerics;

namespace WeaponBuilder.Core;

public sealed record PlacedPart(WeaponPart Part, PartDef Def, Matrix4x4 World, WeaponPart? Parent, string? Socket);
public sealed record PlacedSocket(WeaponPart Owner, SocketDef Socket, Matrix4x4 World, WeaponPart? Occupant);
public sealed record LayoutResult(IReadOnlyList<PlacedPart> Parts, IReadOnlyList<PlacedSocket> Sockets);

/// Where every part and socket of a weapon sits, in the weapon's space (root = identity).
/// System.Numerics matrices are row-vector: in a product, the leftmost applies first.
public static class Layout
{
    public static LayoutResult Compute(Weapon w, PartLibrary lib)
    {
        var parts = new List<PlacedPart>();
        var sockets = new List<PlacedSocket>();
        Place(w.Root, Matrix4x4.Identity, null, null);
        return new LayoutResult(parts, sockets);

        void Place(WeaponPart part, Matrix4x4 world, WeaponPart? parent, string? socketName)
        {
            var def = lib.Get(part.PartId);
            parts.Add(new PlacedPart(part, def, world, parent, socketName));
            foreach (var socket in def.Sockets)
            {
                var socketWorld = SocketLocal(socket) * world;
                part.Children.TryGetValue(socket.Name, out var child);
                sockets.Add(new PlacedSocket(part, socket, socketWorld, child));
                if (child != null)
                    Place(child, ChildWorld(socket, lib.Get(child.PartId), child.RotationDegrees, socketWorld), part, socket.Name);
            }
        }
    }

    /// A part's world transform on a socket whose world transform is socketWorld: fitted scale, then spin about +Y.
    public static Matrix4x4 ChildWorld(SocketDef socket, PartDef def, float rotationDegrees, Matrix4x4 socketWorld) =>
        Matrix4x4.CreateScale(Fitting.Scale(socket, def))
        * Matrix4x4.CreateRotationY(float.DegreesToRadians(rotationDegrees))
        * socketWorld;

    /// A socket's transform relative to its part: rotation (Godot YXZ order), then translation.
    public static Matrix4x4 SocketLocal(SocketDef s) =>
        Matrix4x4.CreateFromYawPitchRoll(
            float.DegreesToRadians(s.RotationDegrees.Y),
            float.DegreesToRadians(s.RotationDegrees.X),
            float.DegreesToRadians(s.RotationDegrees.Z))
        * Matrix4x4.CreateTranslation(s.Position);
}
