using System.Collections.Generic;
using Godot;

namespace WeaponBuilder;

/// The weapon materials, by id. Object-space triplanar (no UVs; textures move with the weapon), flat colours for now.
public static class WeaponMaterials
{
    public static readonly string[] Ids = ["steel", "iron", "wood", "leather", "brass"];

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

    /// The material with this id; unknown ids get steel.
    public static Material Get(string id)
    {
        if (Cache.TryGetValue(id, out var m))
            return m;
        var (color, metallic, roughness) = id switch
        {
            "iron" => (new Color(0.35f, 0.35f, 0.37f), 0.7f, 0.6f),
            "wood" => (new Color(0.45f, 0.3f, 0.18f), 0f, 0.8f),
            "leather" => (new Color(0.33f, 0.2f, 0.12f), 0f, 0.9f),
            "brass" => (new Color(0.75f, 0.58f, 0.25f), 0.8f, 0.4f),
            "steel" => (new Color(0.7f, 0.72f, 0.75f), 0.8f, 0.35f),
            _ => (Color.Color8(0, 0, 0), -1f, 0f),
        };
        if (metallic < 0f)
            return Get("steel");
        m = new StandardMaterial3D
        {
            AlbedoColor = color, Metallic = metallic, Roughness = roughness,
            Uv1Triplanar = true, Uv1WorldTriplanar = false,
        };
        Cache[id] = m;
        return m;
    }
}
