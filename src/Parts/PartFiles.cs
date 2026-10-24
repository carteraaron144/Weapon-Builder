using System.Collections.Generic;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Loads the part library from res://data/parts. Problems are reported with GD.PushError.
public static class PartFiles
{
    public const string Dir = "res://data/parts";

    public static PartLibrary LoadAll()
    {
        var files = new List<(string, string)>();
        foreach (var name in DirAccess.GetFilesAt(Dir))
            if (name.EndsWith(".json"))
                files.Add((name, FileAccess.GetFileAsString($"{Dir}/{name}")));
        var lib = PartLibrary.Load(files, out var errors);
        foreach (var e in errors)
            GD.PushError($"Part file {e}");
        return lib;
    }
}
