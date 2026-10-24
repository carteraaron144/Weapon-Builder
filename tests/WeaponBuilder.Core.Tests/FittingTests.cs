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

public class FittingScaleTests
{
    [Fact]
    public void Scale_UsesTighterAxis() =>
        Assert.Equal(0.5f, Fitting.Scale(Socket("s", ["x"], 1f, 0.2f), Part("p", ["x"], 2f, 0.1f)), 5);

    [Fact]
    public void Scale_ClampsLow() =>
        Assert.Equal(0.25f, Fitting.Scale(Socket("s", ["x"], 0.1f, 0.1f), Part("p", ["x"], 10f, 10f)), 5);

    [Fact]
    public void Scale_ClampsHigh() =>
        Assert.Equal(4f, Fitting.Scale(Socket("s", ["x"], 10f, 10f), Part("p", ["x"], 0.1f, 0.1f)), 5);

    [Fact]
    public void Scale_Equal_IsOne() =>
        Assert.Equal(1f, Fitting.Scale(Socket("s", ["x"], 0.5f, 0.05f), Part("p", ["x"], 0.5f, 0.05f)), 5);

    [Theory]
    [InlineData(90f, 0f, 0f)]       // no step: no spin
    [InlineData(90f, 120f, 120f)]   // nearest multiple
    [InlineData(170f, 120f, 120f)]
    [InlineData(300f, 120f, 0f)]    // 360 wraps to 0
    [InlineData(-90f, 90f, 270f)]
    public void SnapRotation(float degrees, float step, float expected) =>
        Assert.Equal(expected, Fitting.SnapRotation(degrees, step), 3);
}
