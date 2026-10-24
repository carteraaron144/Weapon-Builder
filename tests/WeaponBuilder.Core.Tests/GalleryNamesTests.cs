using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

public class GalleryNamesTests
{
    static readonly DateTime At = new(2026, 10, 24, 14, 5, 9);

    [Fact]
    public void FileStem_Slugifies() => Assert.Equal("big-axe-20261024-140509", GalleryNames.FileStem("Big  Axe!", At));

    [Fact]
    public void FileStem_EmptyName_UsesWeapon() => Assert.Equal("weapon-20261024-140509", GalleryNames.FileStem("  ", At));

    [Fact]
    public void Unique_FreeStem_Unchanged() => Assert.Equal("axe-1", GalleryNames.Unique("axe-1", _ => false));

    [Fact]
    public void Unique_TakenStem_AddsCounter()
    {
        var taken = new HashSet<string> { "axe-1", "axe-1-2" };
        Assert.Equal("axe-1-3", GalleryNames.Unique("axe-1", taken.Contains));
    }
}
