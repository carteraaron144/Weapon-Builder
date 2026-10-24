namespace WeaponBuilder.Core;

/// Every known part definition, by id. Categories keep the order parts were added in.
public sealed partial class PartLibrary
{
    readonly Dictionary<string, PartDef> byId = new();
    readonly List<string> categories = new();

    public PartLibrary(IEnumerable<PartDef> parts)
    {
        foreach (var part in parts)
        {
            byId[part.Id] = part;
            if (!categories.Contains(part.Category))
                categories.Add(part.Category);
        }
    }

    public IEnumerable<PartDef> All => byId.Values;
    public IReadOnlyList<string> Categories => categories;

    public PartDef Get(string id) => byId[id];
    public bool TryGet(string id, out PartDef def) => byId.TryGetValue(id, out def!);
    public IReadOnlyList<PartDef> ByCategory(string category) =>
        byId.Values.Where(p => p.Category == category).ToList();
}
