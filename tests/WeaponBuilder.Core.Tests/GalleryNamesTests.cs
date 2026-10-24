using WeaponBuilder.Core;

namespace WeaponBuilder.Core.Tests;

public class GalleryNamesTests
{
    static readonly DateTime At = new(2026, 10, 24, 14, 5, 9);

    [Fact]
    public void FileStem_Slugifies() => Assert.Equal("big-axe-20261024-140509", GalleryNames.FileStem("Big  Axe!", At));

    [Fact]
    public void FileStem_EmptyName_UsesWeapon() => Assert.Equal("weapon-20261024-140509", GalleryNames.FileStem("  ", At));
}
