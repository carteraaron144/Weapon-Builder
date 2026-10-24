using System.Collections.Generic;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Builds each part's flat-shaded mesh from its primitives, once per part id.
/// The mesh carries the part's default material; views override it with MaterialOverride.
public static class PartMeshFactory
{
    static readonly Dictionary<string, ArrayMesh> Cache = new();

    public static ArrayMesh Get(PartDef def)
    {
        if (Cache.TryGetValue(def.Id, out var mesh))
            return mesh;
        var parts = new List<(Mesh, Transform3D)>();
        foreach (var p in def.Mesh)
        {
            var s = p.Size;
            Mesh? prim = p.Shape switch
            {
                "box" => MeshKit.Box(NumericsBridge.ToGodot(s)),
                "prism" => MeshKit.Prism(s.X, s.Z, s.Y, p.Sides > 2 ? p.Sides : 6),
                "sphere" => MeshKit.Lump(s.X, p.Sides > 2 ? p.Sides : 6, s.Y >= 2 ? (int)s.Y : 3),
                "ring" => MeshKit.Ring(s.X, s.Y, p.Sides > 2 ? p.Sides : 8),
                _ => null,
            };
            if (prim == null)
            {
                GD.PushError($"Part '{def.Id}': unknown mesh shape '{p.Shape}'");
                continue;
            }
            parts.Add((prim, MeshKit.Xf(NumericsBridge.ToGodot(p.Position), NumericsBridge.ToGodot(p.RotationDegrees),
                NumericsBridge.ToGodot(p.Scale))));
        }
        mesh = MeshKit.FlatMesh(new() { [WeaponMaterials.Get(def.DefaultMaterial)] = parts });
        Cache[def.Id] = mesh;
        return mesh;
    }
}
