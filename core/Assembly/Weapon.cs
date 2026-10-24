namespace WeaponBuilder.Core;

/// One placed part in a weapon, with the parts attached to its sockets.
public sealed class WeaponPart(string partId, string material)
{
    public string PartId { get; set; } = partId;
    public string Material { get; set; } = material;
    /// Spin about the socket's axis, in degrees.
    public float RotationDegrees { get; set; }
    /// Attached parts, keyed by socket name.
    public Dictionary<string, WeaponPart> Children { get; } = new();
}

/// A weapon: a tree of parts whose root is a handle.
public sealed class Weapon(string name, WeaponPart root)
{
    public string Name { get; set; } = name;
    public WeaponPart Root { get; set; } = root;
}
