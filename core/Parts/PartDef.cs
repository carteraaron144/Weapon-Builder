using System.Numerics;

namespace WeaponBuilder.Core;

// Conventions: a part's origin is its attach point and it grows along +Y.
// A socket's +Y points away from the part that owns it. Rotations are degrees, Godot YXZ order.

/// One mesh primitive of a part. Size means: box = extents; prism = [top radius, height, bottom radius];
/// sphere = [radius, rings, unused]; ring = [inner radius, outer radius, unused]. Sides is used by prism, sphere and ring.
public sealed record Primitive(string Shape, Vector3 Size, Vector3 Position, Vector3 RotationDegrees, Vector3 Scale, int Sides);

/// A place on a part where another part attaches.
public sealed record SocketDef(string Name, Vector3 Position, Vector3 RotationDegrees,
    IReadOnlyList<string> Accepts, float Length, float Width, bool Required, float RotationStepDegrees);

/// A kind of part, loaded from data/parts/*.json.
public sealed record PartDef(string Id, string Name, string Category, IReadOnlyList<string> Tags,
    float Length, float Width, string DefaultMaterial, IReadOnlyList<SocketDef> Sockets, IReadOnlyList<Primitive> Mesh);
