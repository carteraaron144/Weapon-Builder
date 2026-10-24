using System.Collections.Generic;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The weapon materials, by id, from data/materials.json. Object-space triplanar (no UVs; textures move with the
/// weapon), flat colours for now.
public static class WeaponMaterials
{
    static MaterialLibrary? library;

    /// The materials from data/materials.json, loaded on first use.
    public static MaterialLibrary Library => library ??= MaterialFiles.Load();

    static readonly Dictionary<string, StandardMaterial3D> Cache = new();

    static readonly Dictionary<string, StandardMaterial3D> GhostCache = new();

    /// A see-through copy of a material, for drag previews.
    public static Material Ghost(string id)
    {
        if (GhostCache.TryGetValue(id, out var g))
            return g;
        g = (StandardMaterial3D)Get(id).Duplicate();
        g.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        g.AlbedoColor = g.AlbedoColor with { A = 0.5f };
        GhostCache[id] = g;
        return g;
    }

    /// The material with this id, built from data/materials.json; unknown ids get steel.
    public static Material Get(string id)
    {
        if (Cache.TryGetValue(id, out var m))
            return m;
        if (!Library.TryGet(id, out var def))
            return id == "steel" ? Fallback() : Get("steel");
        var c = def.Color;
        m = new StandardMaterial3D
        {
            AlbedoColor = new Color(c.X, c.Y, c.Z), Metallic = def.Metallic, Roughness = def.Roughness,
            Uv1Triplanar = true, Uv1WorldTriplanar = false,
        };
        Cache[id] = m;
        return m;
    }

    /// Plain grey, for when even steel is missing from the materials file.
    static StandardMaterial3D Fallback() =>
        Cache["steel"] = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.72f, 0.75f), Metallic = 0.8f, Roughness = 0.35f };
}
