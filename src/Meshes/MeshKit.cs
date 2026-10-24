using System.Collections.Generic;
using Godot;

namespace WeaponBuilder;

/// Mesh helpers. FlatMesh is ported from The Mountain's tools/mesh_kit.gd.
public static class MeshKit
{
    /// Merges primitive parts into one mesh, one surface per material, with flat normals:
    /// every face gets a hard edge (low poly, never rounded).
    public static ArrayMesh FlatMesh(Dictionary<Material, List<(Mesh Mesh, Transform3D Xf)>> groups)
    {
        var output = new ArrayMesh();
        foreach (var (material, parts) in groups)
        {
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            foreach (var (mesh, xf) in parts)
                st.AppendFrom(mesh, 0, xf);
            var arrays = st.CommitToArrays();
            var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            if (indices.Length == 0)
            {
                indices = new int[verts.Length];
                for (var i = 0; i < verts.Length; i++)
                    indices[i] = i;
            }

            var flat = new SurfaceTool();
            flat.Begin(Mesh.PrimitiveType.Triangles);
            for (var t = 0; t < indices.Length; t += 3)
            {
                var ia = indices[t];
                var a = verts[ia];
                var b = verts[indices[t + 1]];
                var c = verts[indices[t + 2]];
                var n = (b - a).Cross(c - a);
                if (n.LengthSquared() < 1e-12f)
                    continue; // degenerate (e.g. cone tips)
                n = n.Normalized().Snapped(Vector3.One * 1e-4f); // both triangles of a flat face share a normal and merge
                if (n.Dot(normals[ia]) < 0f)
                    n = -n; // keep the primitive's outward side
                foreach (var v in new[] { a, b, c })
                {
                    flat.SetNormal(n);
                    flat.AddVertex(v);
                }
            }
            flat.Index(); // share vertices within one flat face
            flat.Commit(output);
            output.SurfaceSetMaterial(output.GetSurfaceCount() - 1, material);
        }
        return output;
    }

    public static Transform3D Xf(Vector3 pos, Vector3 rotDegrees, Vector3 scale) =>
        new(Basis.FromEuler(rotDegrees * (Mathf.Pi / 180f)) * Basis.FromScale(scale), pos);

    public static BoxMesh Box(Vector3 size) => new() { Size = size };

    public static CylinderMesh Prism(float topRadius, float bottomRadius, float height, int sides) => new()
    {
        TopRadius = topRadius, BottomRadius = bottomRadius, Height = height, RadialSegments = sides, Rings = 1,
    };

    public static SphereMesh Lump(float radius, int sides, int rings) => new()
    {
        Radius = radius, Height = radius * 2f, RadialSegments = sides, Rings = rings,
    };

    public static TorusMesh Ring(float inner, float outer, int sides) => new()
    {
        InnerRadius = inner, OuterRadius = outer, Rings = sides, RingSegments = 4,
    };
}
