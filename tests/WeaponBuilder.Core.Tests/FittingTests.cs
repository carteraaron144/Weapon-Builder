using WeaponBuilder.Core;
using static WeaponBuilder.Core.Tests.TestParts;

namespace WeaponBuilder.Core.Tests;

public class FittingTests
{
    [Fact]
    public void Accepts_SharedTag_True() =>
        Assert.True(Fitting.Accepts(Socket("top", ["blade", "head"]), Part("axe", ["head"])));

    [Fact]
    public void Accepts_NoSharedTag_False() =>
        Assert.False(Fitting.Accepts(Socket("top", ["blade", "head"]), Part("ball", ["pommel"])));

    [Fact]
    public void Accepts_EmptyAccepts_False() =>
        Assert.False(Fitting.Accepts(Socket("top", []), Part("axe", ["head"])));
}
