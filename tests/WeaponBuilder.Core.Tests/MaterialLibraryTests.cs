using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

public class MaterialLibraryTests
{
    static string Entry(string id, string color = "#808080", string extra = "") =>
        $$"""{ "id": "{{id}}", "name": "N {{id}}", "color": "{{color}}", "metallic": 0.5, "roughness": 0.25, "tags": ["metal"] {{extra}} }""";

    static MaterialLibrary Load(out List<string> errors, params string[] entries) =>
        MaterialLibrary.Load($$"""{ "materials": [ {{string.Join(",", entries)}} ] }""", out errors);

    [Fact]
    public void Load_ReadsEveryFieldInFileOrder()
    {
        var lib = Load(out var errors, Entry("bronze", "#FF8000"), Entry("leather", extra: """, "hidden": true, "tags": ["leather"]"""));
        Assert.Empty(errors);
        Assert.Equal(["bronze", "leather"], lib.All.Select(m => m.Id));
        var bronze = lib.Get("bronze");
        Assert.Equal("N bronze", bronze.Name);
        Assert.Equal(1f, bronze.Color.X, 0.01f);
        Assert.Equal(0.502f, bronze.Color.Y, 0.01f);
        Assert.Equal(0f, bronze.Color.Z, 0.01f);
        Assert.Equal(0.5f, bronze.Metallic);
        Assert.Equal(0.25f, bronze.Roughness);
        Assert.Equal(["metal"], bronze.Tags);
        Assert.False(bronze.Hidden);
        Assert.True(lib.Get("leather").Hidden);
        Assert.Equal(["leather"], lib.Get("leather").Tags);
    }

    [Fact]
    public void Load_DuplicateIdIsSkippedWithAnError()
    {
        var lib = Load(out var errors, Entry("steel", "#111111"), Entry("steel", "#222222"));
        Assert.Contains("steel", Assert.Single(errors));
        Assert.Equal(0x11 / 255f, Assert.Single(lib.All).Color.X, 0.001f);
    }

    [Theory]
    [InlineData("orange")]
    [InlineData("#12345")]
    public void Load_BadColourIsSkippedWithAnError(string color)
    {
        var lib = Load(out var errors, Entry("odd", color), Entry("steel"));
        Assert.Contains("odd", Assert.Single(errors));
        Assert.Equal("steel", Assert.Single(lib.All).Id);
    }

    [Fact]
    public void Load_MissingIdIsSkippedWithAnError()
    {
        var lib = Load(out var errors, """{ "name": "x", "color": "#000000" }""", Entry("steel"));
        Assert.Single(errors);
        Assert.Single(lib.All);
    }

    [Fact]
    public void Load_BrokenJsonGivesAnEmptyLibraryAndAnError()
    {
        var lib = MaterialLibrary.Load("{ nope", out var errors);
        Assert.Single(errors);
        Assert.Empty(lib.All);
    }

    [Fact]
    public void Visible_LeavesOutHiddenMaterials()
    {
        var lib = Load(out _, Entry("a"), Entry("b", extra: """, "hidden": true"""), Entry("c"));
        Assert.Equal(["a", "c"], lib.Visible.Select(m => m.Id));
        Assert.True(lib.TryGet("b", out _));
        Assert.False(lib.TryGet("zzz", out _));
    }
}
