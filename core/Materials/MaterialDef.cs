using System.Numerics;

namespace WeaponBuilder.Core;

/// A material parts can be painted with, loaded from data/materials.json. Color is 0–1 RGB.
/// Hidden materials (leather) can be a part's default but aren't offered in the materials panel.
public sealed record MaterialDef(string Id, string Name, Vector3 Color, float Metallic, float Roughness,
    IReadOnlyList<string> Tags, bool Hidden);
