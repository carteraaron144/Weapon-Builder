using WeaponBuilder.Core;
using static WeaponBuilder.Core.Tests.TestParts;

namespace WeaponBuilder.Core.Tests;

public class WeaponJsonTests
{
    static readonly PartLibrary Lib = new([
        Part("grip", ["handle"], 1f, 0.1f, Socket("top", ["blade", "head"], step: 90f), Socket("bottom", ["pommel"])),
        Part("blade", ["blade"], 1f, 0.1f, Socket("tip", ["head"])),
        Part("axe", ["head"]),
        Part("ball", ["pommel"]),
    ]);

    static Weapon Sample()
    {
        var root = new WeaponPart("grip", "leather");
        var blade = new WeaponPart("blade", "iron") { RotationDegrees = 90 };
        blade.Children["tip"] = new WeaponPart("axe", "steel");
        root.Children["top"] = blade;
        root.Children["bottom"] = new WeaponPart("ball", "brass");
        return new Weapon("Big Axe", root);
    }

    [Fact]
    public void RoundTrip_PreservesTree()
    {
        var back = WeaponJson.Read(WeaponJson.Write(Sample()), Lib, "grip", out var warnings);
        Assert.Empty(warnings);
        Assert.Equal("Big Axe", back.Name);
        Assert.Equal("grip", back.Root.PartId);
        Assert.Equal("leather", back.Root.Material);
        var blade = back.Root.Children["top"];
        Assert.Equal("iron", blade.Material);
        Assert.Equal(90f, blade.RotationDegrees);
        Assert.Equal("axe", blade.Children["tip"].PartId);
        Assert.Equal("brass", back.Root.Children["bottom"].Material);
    }

    [Fact]
    public void Read_UnknownChildPart_DropsSubtreeWithWarning()
    {
        var json = WeaponJson.Write(Sample()).Replace("\"blade\"", "\"gone\"");
        var back = WeaponJson.Read(json, Lib, "grip", out var warnings);
        Assert.False(back.Root.Children.ContainsKey("top"));
        Assert.True(back.Root.Children.ContainsKey("bottom"));
        Assert.Contains("gone", Assert.Single(warnings));
    }

    [Fact]
    public void Read_UnknownRoot_UsesDefaultRootWithWarning()
    {
        var json = WeaponJson.Write(Sample()).Replace("\"grip\"", "\"gone\"");
        var back = WeaponJson.Read(json, Lib, "grip", out var warnings);
        Assert.Equal("grip", back.Root.PartId);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Read_ChildOnSocketThatNoLongerAccepts_DroppedWithWarning()
    {
        var w = Sample();
        w.Root.Children["bottom"] = new WeaponPart("axe", "steel"); // bottom takes pommels only
        var back = WeaponJson.Read(WeaponJson.Write(w), Lib, "grip", out var warnings);
        Assert.False(back.Root.Children.ContainsKey("bottom"));
        Assert.Single(warnings);
    }

    [Fact]
    public void Read_BadJson_ReturnsDefaultWeaponWithWarning()
    {
        var back = WeaponJson.Read("{ nope", Lib, "grip", out var warnings);
        Assert.Equal("grip", back.Root.PartId);
        Assert.Single(warnings);
    }

    [Fact]
    public void Read_NullChildIsDroppedWithAWarning()
    {
        var json = """{ "version": 1, "name": "X", "root": { "part": "grip", "children": { "top": null } } }""";
        var w = WeaponJson.Read(json, Lib, "grip", out var warnings);
        Assert.Empty(w.Root.Children);
        Assert.Contains(warnings, x => x.Contains("grip.top"));
    }

    [Fact]
    public void Read_RotationSnapsToTheSocketStep()
    {
        var json = """{ "version": 1, "root": { "part": "grip", "children": { "top": { "part": "blade", "rotation": 45 } } } }""";
        var w = WeaponJson.Read(json, Lib, "grip", out _);
        Assert.Equal(90f, w.Root.Children["top"].RotationDegrees);
    }
}
