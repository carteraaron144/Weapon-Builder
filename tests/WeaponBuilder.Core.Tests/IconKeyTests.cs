using System.Numerics;
using WeaponBuilder.Core;
using static WeaponBuilder.Core.Tests.TestParts;

namespace WeaponBuilder.Core.Tests;

public class IconKeyTests
{
    static PartDef Blade(float width = 0.1f) =>
        Part("blade", ["blade"], 1f, 0.1f) with
        {
            Mesh = [new Primitive("box", new Vector3(width, 1f, 0.02f), Vector3.Zero, Vector3.Zero, Vector3.One, 0)],
        };

    [Fact]
    public void IconKey_StartsWithThePartId() =>
        Assert.Matches("^blade-[0-9a-f]{16}$", IconKey.For(Blade(), "v1"));

    [Fact]
    public void IconKey_IsStableForTheSamePart() =>
        Assert.Equal(IconKey.For(Blade(), "v1"), IconKey.For(Blade(), "v1"));

    [Fact]
    public void IconKey_ChangesWhenAPrimitiveSizeChanges() =>
        Assert.NotEqual(IconKey.For(Blade(0.1f), "v1"), IconKey.For(Blade(0.2f), "v1"));

    [Fact]
    public void IconKey_ChangesWhenTheDefaultMaterialChanges() =>
        Assert.NotEqual(IconKey.For(Blade(), "v1"), IconKey.For(Blade() with { DefaultMaterial = "gold" }, "v1"));

    [Fact]
    public void IconKey_ChangesWithTheStyle() =>
        Assert.NotEqual(IconKey.For(Blade(), "v1"), IconKey.For(Blade(), "v2"));
}
