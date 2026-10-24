using System.Text;

namespace WeaponBuilder.Core;

public static class GalleryNames
{
    /// "<slug>-<yyyyMMdd-HHmmss>": lowercase letters and digits, runs of anything else become one '-'.
    public static string FileStem(string weaponName, DateTime at)
    {
        var slug = new StringBuilder();
        foreach (var c in weaponName.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
                slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }
        var s = slug.ToString().Trim('-');
        return $"{(s.Length == 0 ? "weapon" : s)}-{at:yyyyMMdd-HHmmss}";
    }
}
