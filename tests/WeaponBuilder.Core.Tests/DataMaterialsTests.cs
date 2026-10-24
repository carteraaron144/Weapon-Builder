using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

/// Checks the shipped data/materials.json.
public class DataMaterialsTests
{
    static MaterialLibrary LoadShipped(out List<string> errors) =>
        MaterialLibrary.Load(File.ReadAllText(Path.Combine(RepoFiles.Root, "data", "materials.json")), out errors);

    [Fact]
    public void ShippedMaterialsLoadWithoutErrors()
    {
        LoadShipped(out var errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void Panel_IsTheSevenAgreedMaterialsInOrder() =>
        Assert.Equal(["bronze", "iron", "steel", "gold", "mithril", "obsidian", "wood"],
            LoadShipped(out _).Visible.Select(m => m.Id));

    [Fact]
    public void EveryPartsDefaultMaterialExists()
    {
        var materials = LoadShipped(out _);
        Assert.All(DataPartsTests.LoadShipped(out _).All,
            p => Assert.True(materials.TryGet(p.DefaultMaterial, out _), $"{p.Id}: unknown default material '{p.DefaultMaterial}'"));
    }
}
