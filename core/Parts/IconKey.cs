using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WeaponBuilder.Core;

/// The cache name for a part's icon: "<id>-<16 hex chars>", hashed from the whole part definition and the
/// icon style, so editing a part (or how icons are drawn) gives a new name and the icon is rendered again.
public static class IconKey
{
    // IncludeFields: Vector3's X, Y and Z are fields, and would otherwise serialize as {}.
    static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static string For(PartDef def, string style)
    {
        var text = JsonSerializer.Serialize(def, Options) + "\n" + style;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return $"{def.Id}-{hash[..16]}";
    }
}
