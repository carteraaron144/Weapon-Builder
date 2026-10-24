using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

/// Checks the shipped part files in data/parts.
public class DataPartsTests
{
    static readonly string[] Categories = ["Handles", "Guards", "Pommels", "Blades & Heads", "Flanges"];

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
        Assert.Equal(18, lib.All.Count());
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

    [Fact]
    public void DataParts_EveryHandleRequiresItsTop() =>
        Assert.All(LoadShipped(out _).All.Where(p => p.Tags.Contains(WeaponEditor.HandleTag)),
            h => Assert.True(h.Sockets.Single(s => s.Name == "top").Required, $"{h.Id}.top should be required"));

    [Fact]
    public void DataParts_MaceHeadHasSixFlangeSocketsPointingOutward()
    {
        var lib = LoadShipped(out _);
        var sockets = lib.Get("mace_head").Sockets.Where(s => s.Accepts.Contains("flange")).ToList();
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
    public void DataParts_FlangesFitTheMaceHead()
    {
        var lib = LoadShipped(out _);
        var socket = lib.Get("mace_head").Sockets.First(s => s.Accepts.Contains("flange"));
        var flanges = lib.ByCategory("Flanges");
        Assert.Equal(3, flanges.Count);
        Assert.All(flanges, f => Assert.True(Fitting.Accepts(socket, f), $"{f.Id} doesn't fit"));
    }
}
