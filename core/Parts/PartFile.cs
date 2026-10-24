using System.Numerics;

namespace WeaponBuilder.Core;

// The shape of a data/parts/*.json file. Vectors are [x, y, z]; rotations are degrees.
sealed class PartFile
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public float Length { get; set; }
    public float Width { get; set; }
    public string? DefaultMaterial { get; set; }
    public List<SocketFile>? Sockets { get; set; }
    public List<PrimitiveFile>? Mesh { get; set; }
    public List<string>? Materials { get; set; }

    public sealed class SocketFile
    {
        public string? Name { get; set; }
        public float[]? Position { get; set; }
        public float[]? Rotation { get; set; }
        public List<string>? Accepts { get; set; }
        public float Length { get; set; }
        public float Width { get; set; }
        public bool Required { get; set; }
        public float RotationStep { get; set; }
    }

    public sealed class PrimitiveFile
    {
        public string? Shape { get; set; }
        public float[]? Size { get; set; }
        public float[]? Position { get; set; }
        public float[]? Rotation { get; set; }
        public float[]? Scale { get; set; }
        public int Sides { get; set; }
    }

    /// Converts to a PartDef, or throws FormatException naming the problem.
    public PartDef ToDef()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new FormatException("missing id");
        if (Length <= 0 || Width <= 0) throw new FormatException($"part '{Id}' needs a positive length and width");
        var sockets = (Sockets ?? []).Select(s =>
        {
            if (string.IsNullOrWhiteSpace(s.Name)) throw new FormatException($"part '{Id}' has a socket with no name");
            if (s.Accepts == null || s.Accepts.Count == 0) throw new FormatException($"socket '{s.Name}' of '{Id}' accepts nothing");
            if (s.Length <= 0 || s.Width <= 0) throw new FormatException($"socket '{s.Name}' of '{Id}' needs a positive length and width");
            return new SocketDef(s.Name, Vec(s.Position, Vector3.Zero), Vec(s.Rotation, Vector3.Zero),
                s.Accepts, s.Length, s.Width, s.Required, s.RotationStep);
        }).ToList();
        var mesh = (Mesh ?? []).Select(p => new Primitive(p.Shape ?? "box", Vec(p.Size, Vector3.One),
            Vec(p.Position, Vector3.Zero), Vec(p.Rotation, Vector3.Zero), Vec(p.Scale, Vector3.One), p.Sides)).ToList();
        return new PartDef(Id, Name ?? Id, Category ?? "Other", Tags ?? [], Length, Width,
            DefaultMaterial ?? "steel", sockets, mesh, Materials);
    }

    static Vector3 Vec(float[]? v, Vector3 fallback) => v switch
    {
        null => fallback,
        { Length: 3 } => new Vector3(v[0], v[1], v[2]),
        _ => throw new FormatException("vectors need 3 numbers"),
    };
}
