using System.Text.Json;

namespace WeaponBuilder.Core;

public sealed partial class PartLibrary
{
    /// Builds a library from part files. Files whose name starts with '_' are templates and skipped.
    /// A file that doesn't parse, fails validation or repeats an id is skipped with one error naming it.
    public static PartLibrary Load(IEnumerable<(string FileName, string Json)> files, out List<string> errors)
    {
        errors = new();
        var parts = new List<PartDef>();
        foreach (var (fileName, json) in files)
        {
            if (Path.GetFileName(fileName).StartsWith('_'))
                continue;
            try
            {
                var def = (JsonSerializer.Deserialize<PartFile>(json, JsonOptions.Value)
                           ?? throw new FormatException("empty file")).ToDef();
                if (parts.Any(p => p.Id == def.Id))
                    throw new FormatException($"duplicate part id '{def.Id}'");
                parts.Add(def);
            }
            catch (Exception e) when (e is JsonException or FormatException)
            {
                errors.Add($"{fileName}: {e.Message}");
            }
        }
        return new PartLibrary(parts);
    }
}
