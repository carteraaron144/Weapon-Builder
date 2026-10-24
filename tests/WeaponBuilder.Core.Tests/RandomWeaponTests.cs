using WeaponBuilder.Core;
using static WeaponBuilder.Core.Tests.TestParts;

namespace WeaponBuilder.Core.Tests;

public class RandomWeaponTests
{
    // b1's tip takes blades, so it can recurse forever without the depth cap.
    static readonly PartLibrary Lib = new([
        Part("h", ["handle"], 1f, 0.1f, Socket("top", ["blade"], step: 90f, required: true), Socket("bottom", ["pommel"])),
        Part("b1", ["blade"], 1f, 0.1f, Socket("tip", ["blade"], step: 120f)),
        Part("b2", ["blade"]),
        Part("p", ["pommel"]),
    ]);

    static IEnumerable<int> Seeds(int n) => Enumerable.Range(0, n);

    static Weapon Make(int seed, RandomOptions? options = null) => RandomWeapon.Build(Lib, new Random(seed), options);

    /// Every part with its depth and the socket it sits on (null for the root).
    static IEnumerable<(WeaponPart Part, int Depth, SocketDef? Socket)> Walk(Weapon w)
    {
        var stack = new Stack<(WeaponPart, int, SocketDef?)>([(w.Root, 0, null)]);
        while (stack.Count > 0)
        {
            var (part, depth, socket) = stack.Pop();
            yield return (part, depth, socket);
            var def = Lib.Get(part.PartId);
            foreach (var (name, child) in part.Children)
                stack.Push((child, depth + 1, def.Sockets.Single(s => s.Name == name)));
        }
    }

    [Fact]
    public void SameSeedSameWeapon() => Assert.Equal(WeaponJson.Write(Make(7)), WeaponJson.Write(Make(7)));

    [Fact]
    public void DifferentSeedsDiffer() =>
        Assert.True(Seeds(20).Select(s => WeaponJson.Write(Make(s))).Distinct().Count() >= 2);

    [Fact]
    public void RequiredSocketsAreFilled() =>
        Assert.All(Seeds(50), s => Assert.True(Make(s).Root.Children.ContainsKey("top")));

    [Fact]
    public void OptionalChanceZeroLeavesOptionalEmpty() =>
        Assert.All(Seeds(50), s => Assert.DoesNotContain(Walk(Make(s, new RandomOptions(OptionalChance: 0))),
            x => x.Socket is { Required: false }));

    [Fact]
    public void OptionalChanceOneFillsEveryOptional() =>
        Assert.All(Seeds(50), s => Assert.True(Make(s, new RandomOptions(OptionalChance: 1)).Root.Children.ContainsKey("bottom")));

    [Fact]
    public void NoPartDeeperThanMaxDepth() =>
        Assert.Equal(3, Seeds(50).SelectMany(s => Walk(Make(s, new RandomOptions(MaxDepth: 3, OptionalChance: 1)))).Max(x => x.Depth));

    [Fact]
    public void RequiredSocketAtTheCapStaysEmpty() =>
        Assert.All(Seeds(10), s => Assert.Empty(Make(s, new RandomOptions(MaxDepth: 0)).Root.Children));

    [Fact]
    public void EveryPartFitsItsSocket() =>
        Assert.All(Seeds(50).SelectMany(s => Walk(Make(s, new RandomOptions(OptionalChance: 1)))).Where(x => x.Socket != null),
            x => Assert.True(Fitting.Accepts(x.Socket!, Lib.Get(x.Part.PartId))));

    [Fact]
    public void RotationsAreStepMultiples()
    {
        var placed = Seeds(50).SelectMany(s => Walk(Make(s, new RandomOptions(OptionalChance: 1)))).Where(x => x.Socket != null).ToList();
        Assert.All(placed, x => Assert.Equal(Fitting.SnapRotation(x.Part.RotationDegrees, x.Socket!.RotationStepDegrees), x.Part.RotationDegrees));
        Assert.Contains(placed, x => x.Part.RotationDegrees != 0f); // spins are actually picked
    }

    [Fact]
    public void HandlesOnlyLibraryGivesBareHandle()
    {
        var w = RandomWeapon.Build(new PartLibrary([Part("h", ["handle"], 1f, 0.1f, Socket("top", ["blade"], required: true))]), new Random(1));
        Assert.Equal("h", w.Root.PartId);
        Assert.Empty(w.Root.Children);
    }

    [Fact]
    public void NoHandlesThrows() =>
        Assert.Throws<InvalidOperationException>(() => RandomWeapon.Build(new PartLibrary([Part("b", ["blade"])]), new Random(1)));
}
