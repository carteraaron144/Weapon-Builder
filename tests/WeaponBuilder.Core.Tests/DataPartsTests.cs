using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

/// Checks the shipped part files in data/parts.
public class DataPartsTests
{
    static readonly string[] Categories = ["Handles", "Guards", "Pommels", "Blades & Heads"];

    internal static PartLibrary LoadShipped(out List<string> errors)
    {
        var files = Directory.GetFiles(Path.Combine(RepoFiles.Root, "data", "parts"), "*.json")
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));
        return PartLibrary.Load(files, out errors);
    }

    [Fact]
    public void AllShippedPartsLoadWithoutErrors()
    {
        var lib = LoadShipped(out var errors);
        Assert.Empty(errors);
        Assert.Equal(21, lib.All.Count());
    }

    [Fact]
    public void DataParts_EveryHandleHasATopSocket()
    {
        var handles = LoadShipped(out _).All.Where(p => p.Tags.Contains(WeaponEditor.HandleTag)).ToList();
        Assert.NotEmpty(handles);
        Assert.All(handles, h => Assert.Contains(h.Sockets, s => s.Name == "top"));
    }

    [Fact]
    public void DataParts_CategoriesAreThePaletteTabs() =>
        Assert.All(LoadShipped(out _).All, p => Assert.Contains(p.Category, Categories));

    [Fact]
    public void DataParts_EverySocketAcceptsSomeShippedPart()
    {
        var lib = LoadShipped(out _);
        Assert.All(lib.All.SelectMany(p => p.Sockets), s => Assert.Contains(lib.All, p => Fitting.Accepts(s, p)));
    }

    // Randomize always fills required sockets, so no random weapon is a bare handle. Battleaxe hafts require
    // their side socket instead of the top.
    [Fact]
    public void DataParts_EveryHandleHasARequiredSocket() =>
        Assert.All(LoadShipped(out _).All.Where(p => p.Tags.Contains(WeaponEditor.HandleTag)),
            h => Assert.True(h.Sockets.Any(s => s.Required), $"{h.Id} has no required socket"));

    [Theory]
    [InlineData("battleaxe_haft", 1)]
    [InlineData("great_axe_haft", 2)]
    public void DataParts_AxeHaftSideSocketsPointSideways(string id, int count)
    {
        var lib = LoadShipped(out _);
        var sides = lib.Get(id).Sockets.Where(s => s.Name.StartsWith("side")).ToList();
        Assert.Equal(count, sides.Count);
        Assert.All(sides, s => Assert.True(s.Required, $"{id}.{s.Name} should be required"));
        var dirs = sides.Select(s => System.Numerics.Vector3.TransformNormal(System.Numerics.Vector3.UnitY, Layout.SocketLocal(s))).ToList();
        foreach (var (s, d) in sides.Zip(dirs))
        {
            Assert.True(MathF.Abs(d.Y) < 1e-4f, $"{s.Name} points along the haft");
            Assert.True(System.Numerics.Vector3.Dot(d, System.Numerics.Vector3.Normalize(s.Position with { Y = 0 })) > 0.999f,
                $"{s.Name} doesn't point away from the haft");
        }
        if (count == 2)
            Assert.True(System.Numerics.Vector3.Dot(dirs[0], dirs[1]) < -0.999f, "the two sides should be opposite");
    }

    [Fact]
    public void DataParts_CurvedAxeHeadFitsTheSideSocketsAtFullSize()
    {
        var lib = LoadShipped(out _);
        var head = lib.Get("curved_axe_head");
        foreach (var id in new[] { "battleaxe_haft", "great_axe_haft" })
        {
            var side = lib.Get(id).Sockets.First(s => s.Name.StartsWith("side"));
            Assert.True(Fitting.Accepts(side, head), $"doesn't fit {id}.{side.Name}");
            Assert.InRange(Fitting.Scale(side, head), 1f, 1.5f);
        }
    }

    // Every blade, head and flange goes on every socket except guard and pommel sockets.
    [Fact]
    public void DataParts_EveryWeaponPartFitsEveryWeaponSocket()
    {
        var lib = LoadShipped(out _);
        var parts = lib.ByCategory("Blades & Heads");
        var sockets = lib.All.SelectMany(p => p.Sockets.Select(s => (p.Id, s)))
            .Where(x => !x.s.Accepts.Contains("crossguard") && !x.s.Accepts.Contains("pommel")).ToList();
        Assert.Equal(14, sockets.Count);
        foreach (var (owner, s) in sockets)
            foreach (var part in parts)
                Assert.True(Fitting.Accepts(s, part), $"{part.Id} doesn't fit {owner}.{s.Name}");
    }

    [Fact]
    public void DataParts_MaceHeadHasSixFlangeSocketsPointingOutward()
    {
        var lib = LoadShipped(out _);
        var sockets = lib.Get("mace_head").Sockets.ToList();
        Assert.Equal(6, sockets.Count);
        var dirs = sockets.Select(s => System.Numerics.Vector3.TransformNormal(System.Numerics.Vector3.UnitY, Layout.SocketLocal(s))).ToList();
        foreach (var (s, d) in sockets.Zip(dirs))
        {
            Assert.True(MathF.Abs(d.Y) < 1e-4f, $"{s.Name} points along the haft");
            var radial = System.Numerics.Vector3.Normalize(s.Position with { Y = 0 });
            Assert.True(System.Numerics.Vector3.Dot(d, radial) > 0.999f, $"{s.Name} doesn't point away from the head");
        }
        var angles = dirs.Select(d => MathF.Atan2(d.X, d.Z) * 180f / MathF.PI).Select(a => (a + 360f) % 360f).Order().ToList();
        for (var i = 1; i < angles.Count; i++)
            Assert.InRange(angles[i] - angles[i - 1], 59.9f, 60.1f);
    }

    [Fact]
    public void DataParts_FlangesFitTheMaceHeadAtFullSize()
    {
        var lib = LoadShipped(out _);
        var socket = lib.Get("mace_head").Sockets[0];
        var flanges = lib.All.Where(p => p.Tags.Contains("flange")).ToList();
        Assert.Equal(3, flanges.Count);
        Assert.All(flanges, f => Assert.True(Fitting.Accepts(socket, f), $"{f.Id} doesn't fit"));
        Assert.All(flanges, f => Assert.InRange(Fitting.Scale(socket, f), 0.99f, 1.01f));
    }

    [Fact]
    public void DataParts_SwordBladesFitTheMaceHead()
    {
        var lib = LoadShipped(out _);
        var socket = lib.Get("mace_head").Sockets[0];
        foreach (var id in new[] { "straight_blade", "broad_blade" })
        {
            var blade = lib.Get(id);
            Assert.True(Fitting.Accepts(socket, blade), $"{id} doesn't fit");
            Assert.InRange(blade.Length * Fitting.Scale(socket, blade), 0.3f, 0.4f);
        }
    }
}
