using System.Text.Json;

namespace WeaponBuilder.Core;

static class JsonOptions
{
    public static readonly JsonSerializerOptions Value = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };
}
