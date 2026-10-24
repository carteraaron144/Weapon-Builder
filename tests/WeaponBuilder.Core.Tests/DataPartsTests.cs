using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

/// Checks the shipped part files in data/parts.
public class DataPartsTests
{
    static readonly string[] Categories = ["Handles", "Guards", "Pommels", "Blades & Heads"];

    static PartLibrary LoadShipped(out List<string> errors)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var files = Directory.GetFiles(Path.Combine(dir.FullName, "data", "parts"), "*.json")
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));
        return PartLibrary.Load(files, out errors);
    }

    [Fact]
    public void AllShippedPartsLoadWithoutErrors()
    {
        var lib = LoadShipped(out var errors);
        Assert.Empty(errors);
        Assert.Equal(12, lib.All.Count());
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
}
