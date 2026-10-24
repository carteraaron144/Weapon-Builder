using System.Collections.Generic;
using Godot;

namespace WeaponBuilder;

/// The mesh behind a part's outline (ported from The Mountain's outline_shell.gd; outline.gdshader inflates it).
/// Flat-shaded meshes have a copy of each corner per face, which would drift apart when pushed out along their
/// normals; a shell has one vertex per position with the average normal of its faces, so it inflates in one piece.
public static class OutlineShell
{
    const float Weld = 0.0005f; // vertices closer than this (metres) are one corner

    static readonly Dictionary<Mesh, ArrayMesh?> Shells = new();
    static readonly Dictionary<Color, ShaderMaterial> Materials = new();

    /// The shell for mesh, built the first time it's asked for. Null if the mesh has no triangles.
    public static ArrayMesh? For(Mesh mesh)
    {
        if (!Shells.TryGetValue(mesh, out var shell))
            Shells[mesh] = shell = Build(mesh);
        return shell;
    }

    /// The outline material in a colour, shared by everything that asks for that colour.
    public static ShaderMaterial Material(Color color)
    {
        if (!Materials.TryGetValue(color, out var m))
        {
            m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/outline.gdshader") };
            m.SetShaderParameter("color", color);
            Materials[color] = m;
        }
        return m;
    }

    static ArrayMesh? Build(Mesh mesh)
    {
        var faces = mesh.GetFaces(); // every surface's triangles, three vertices each
        var cornerOf = new Dictionary<Vector3I, int>();
        var vertices = new List<Vector3>();
        var faceNormals = new List<List<Vector3>>(); // per corner: the distinct normals of its faces
        var indices = new List<int>();
        for (var t = 0; t + 2 < faces.Length; t += 3)
        {
            var normal = (faces[t + 2] - faces[t]).Cross(faces[t + 1] - faces[t]).Normalized();
            if (normal == Vector3.Zero)
                continue; // degenerate triangle
            for (var i = 0; i < 3; i++)
            {
                var vertex = faces[t + i];
                var key = (Vector3I)(vertex / Weld).Round();
                if (!cornerOf.TryGetValue(key, out var corner))
                {
                    cornerOf[key] = corner = vertices.Count;
                    vertices.Add(vertex);
                    faceNormals.Add(new List<Vector3>());
                }
                indices.Add(corner);
                // Count each facing once, so a flat face cut into many triangles doesn't outweigh the others.
                if (!faceNormals[corner].Exists(n => n.Dot(normal) > 0.999f))
                    faceNormals[corner].Add(normal);
            }
        }
        if (indices.Count == 0)
            return null;

        var normals = new Vector3[vertices.Count];
        for (var c = 0; c < vertices.Count; c++)
        {
            var sum = Vector3.Zero;
            foreach (var n in faceNormals[c])
                sum += n;
            normals[c] = sum.Length() > 0.001f ? sum.Normalized() : faceNormals[c][0];
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var shell = new ArrayMesh();
        shell.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return shell;
    }
}
