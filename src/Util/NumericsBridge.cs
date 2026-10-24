using Godot;

namespace WeaponBuilder;

/// Converts core (System.Numerics) values to Godot ones.
public static class NumericsBridge
{
    public static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    /// System.Numerics matrices are row-vector, so their rows are Godot's basis columns.
    public static Transform3D ToGodot(System.Numerics.Matrix4x4 m) => new(
        new Basis(new Vector3(m.M11, m.M12, m.M13), new Vector3(m.M21, m.M22, m.M23), new Vector3(m.M31, m.M32, m.M33)),
        new Vector3(m.M41, m.M42, m.M43));

    public static System.Numerics.Matrix4x4 ToNumerics(Transform3D t) => new(
        t.Basis.Column0.X, t.Basis.Column0.Y, t.Basis.Column0.Z, 0,
        t.Basis.Column1.X, t.Basis.Column1.Y, t.Basis.Column1.Z, 0,
        t.Basis.Column2.X, t.Basis.Column2.Y, t.Basis.Column2.Z, 0,
        t.Origin.X, t.Origin.Y, t.Origin.Z, 1);
}
