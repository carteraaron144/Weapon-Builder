using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace WeaponBuilder.Core;

/// Every known material, in file order.
public sealed class MaterialLibrary
{
    readonly List<MaterialDef> all = new();
    readonly Dictionary<string, MaterialDef> byId = new();

    public MaterialLibrary(IEnumerable<MaterialDef> materials)
    {
        foreach (var m in materials)
        {
            all.Add(m);
            byId[m.Id] = m;
        }
    }

    public IReadOnlyList<MaterialDef> All => all;
    /// The materials the panel offers: all but the hidden ones.
    public IReadOnlyList<MaterialDef> Visible => all.Where(m => !m.Hidden).ToList();

    public MaterialDef Get(string id) => byId[id];
    public bool TryGet(string id, out MaterialDef def) => byId.TryGetValue(id, out def!);

    /// Reads a materials file: { "materials": [ { id, name, color: "#RRGGBB", metallic, roughness, tags, hidden? } ] }.
    /// An entry with no id, a repeated id or a bad colour is skipped with an error; unreadable JSON gives an empty library.
    public static MaterialLibrary Load(string json, out List<string> errors)
    {
        errors = new();
        MaterialsFile? file;
        try
        {
            file = JsonSerializer.Deserialize<MaterialsFile>(json, JsonOptions.Value);
        }
        catch (JsonException e)
        {
            errors.Add($"Couldn't read materials: {e.Message}");
            return new MaterialLibrary([]);
        }
        var defs = new List<MaterialDef>();
        var entries = file?.Materials ?? [];
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (string.IsNullOrWhiteSpace(e.Id))
                errors.Add($"Material {i} has no id");
            else if (defs.Any(d => d.Id == e.Id))
                errors.Add($"Duplicate material id '{e.Id}'");
            else if (ParseColor(e.Color) is not { } color)
                errors.Add($"Material '{e.Id}' has a bad colour '{e.Color}' (want #RRGGBB)");
            else
                defs.Add(new MaterialDef(e.Id, e.Name ?? e.Id, color, e.Metallic, e.Roughness, e.Tags ?? [], e.Hidden));
        }
        return new MaterialLibrary(defs);
    }

    static Vector3? ParseColor(string? s)
    {
        if (s is not { Length: 7 } || s[0] != '#'
            || !int.TryParse(s.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return null;
        return new Vector3((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF) / 255f;
    }

    sealed class MaterialsFile
    {
        public List<Entry>? Materials { get; set; }
    }

    sealed class Entry
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public float Metallic { get; set; }
        public float Roughness { get; set; }
        public List<string>? Tags { get; set; }
        public bool Hidden { get; set; }
    }
}
