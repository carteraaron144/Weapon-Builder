using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Loads the material library from res://data/materials.json. Problems are reported with GD.PushError.
public static class MaterialFiles
{
    public const string Path = "res://data/materials.json";

    public static MaterialLibrary Load()
    {
        var lib = MaterialLibrary.Load(FileAccess.GetFileAsString(Path), out var errors);
        foreach (var e in errors)
            GD.PushError($"{Path}: {e}");
        return lib;
    }
}
