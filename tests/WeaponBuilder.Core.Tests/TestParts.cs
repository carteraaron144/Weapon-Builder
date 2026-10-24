using System.Numerics;
using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

/// Builders for small part definitions used across tests.
static class TestParts
{
    public static SocketDef Socket(string name, string[] accepts, float length = 1f, float width = 0.1f,
        Vector3? position = null, Vector3? rotation = null, float step = 0f, bool required = false) =>
        new(name, position ?? Vector3.Zero, rotation ?? Vector3.Zero, accepts, length, width, required, step);

    public static PartDef Part(string id, string[] tags, float length = 1f, float width = 0.1f,
        params SocketDef[] sockets) =>
        new(id, id, "Test", tags, length, width, "steel", sockets, Array.Empty<Primitive>(), null);

    /// Steel, iron and (hidden) leather.
    public static readonly MaterialLibrary Materials = new([Mat("steel"), Mat("iron"), Mat("leather", "leather") with { Hidden = true }]);

    /// A plain material for tests.
    public static MaterialDef Mat(string id, params string[] tags) =>
        new(id, id, new Vector3(0.5f), 0.5f, 0.5f, tags.Length > 0 ? tags : ["metal"], false);
}
