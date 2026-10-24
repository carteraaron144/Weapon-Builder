using System.Text.Json;

namespace WeaponBuilder.Core;

/// Saves and loads weapons as JSON: { version, name, root: { part, material, rotation, children: { socket: {...} } } }.
public static class WeaponJson
{
    public const int Version = 1;
    public const string DefaultName = "Unnamed Weapon";

    sealed class FileDto
    {
        public int Version { get; set; }
        public string? Name { get; set; }
        public NodeDto? Root { get; set; }
    }

    sealed class NodeDto
    {
        public string? Part { get; set; }
        public string? Material { get; set; }
        public float Rotation { get; set; }
        public Dictionary<string, NodeDto?>? Children { get; set; }
    }

    public static string Write(Weapon w) =>
        JsonSerializer.Serialize(new FileDto { Version = Version, Name = w.Name, Root = ToDto(w.Root) }, JsonOptions.Value);

    /// Reads a weapon, keeping only what still fits the library. Parts no longer known, and parts on sockets that
    /// no longer exist or accept them, are dropped with their subtree and a warning. An unknown root, or a file that
    /// doesn't parse, falls back to a bare defaultRootId handle.
    public static Weapon Read(string json, PartLibrary lib, string defaultRootId, out List<string> warnings)
    {
        warnings = new();
        FileDto? file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(json, JsonOptions.Value);
        }
        catch (JsonException e)
        {
            warnings.Add($"Couldn't read the weapon: {e.Message}");
            file = null;
        }

        var rootDto = file?.Root;
        WeaponPart root;
        if (rootDto?.Part != null && lib.TryGet(rootDto.Part, out var rootDef) && rootDef.Tags.Contains(WeaponEditor.HandleTag))
        {
            root = FromDto(rootDto, rootDef);
        }
        else
        {
            if (file != null)
                warnings.Add($"Unknown handle '{rootDto?.Part}', using '{defaultRootId}'");
            root = new WeaponPart(defaultRootId, lib.Get(defaultRootId).DefaultMaterial);
        }
        if (rootDto?.Children != null)
            AddChildren(root, rootDto.Children, lib, warnings);
        return new Weapon(string.IsNullOrWhiteSpace(file?.Name) ? DefaultName : file.Name, root);
    }

    static void AddChildren(WeaponPart parent, Dictionary<string, NodeDto?> children, PartLibrary lib, List<string> warnings)
    {
        var parentDef = lib.Get(parent.PartId);
        foreach (var (socketName, dto) in children)
        {
            if (dto == null)
            {
                warnings.Add($"Empty entry on '{parent.PartId}.{socketName}' was dropped");
                continue;
            }
            var socket = parentDef.Sockets.FirstOrDefault(s => s.Name == socketName);
            if (dto.Part == null || !lib.TryGet(dto.Part, out var def))
            {
                warnings.Add($"Unknown part '{dto.Part}' on '{parent.PartId}.{socketName}' was dropped");
                continue;
            }
            if (socket == null || !Fitting.Accepts(socket, def))
            {
                warnings.Add($"'{dto.Part}' no longer fits '{parent.PartId}.{socketName}' and was dropped");
                continue;
            }
            var part = FromDto(dto, def);
            parent.Children[socketName] = part;
            if (dto.Children != null)
                AddChildren(part, dto.Children, lib, warnings);
        }
    }

    static WeaponPart FromDto(NodeDto dto, PartDef def) =>
        new(def.Id, dto.Material ?? def.DefaultMaterial) { RotationDegrees = dto.Rotation };

    static NodeDto ToDto(WeaponPart p) => new()
    {
        Part = p.PartId,
        Material = p.Material,
        Rotation = p.RotationDegrees,
        Children = p.Children.Count == 0 ? null : p.Children.ToDictionary(c => c.Key, c => (NodeDto?)ToDto(c.Value)),
    };
}
