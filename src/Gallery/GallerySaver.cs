using System;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Writes finished weapons to the gallery: <dir>/<stem>.json (WeaponJson) and <stem>.png (thumbnail).
public static class GallerySaver
{
    public const string DefaultDir = "user://gallery";
    public const int ThumbnailSize = 256;

    /// Saves the weapon and its thumbnail; returns the file stem (never overwrites an earlier save).
    public static string Save(Weapon w, Image thumbnail, string dir = DefaultDir)
    {
        DirAccess.MakeDirRecursiveAbsolute(dir);
        var stem = GalleryNames.Unique(GalleryNames.FileStem(w.Name, DateTime.Now),
            s => FileAccess.FileExists($"{dir}/{s}.json"));
        using (var file = FileAccess.Open($"{dir}/{stem}.json", FileAccess.ModeFlags.Write))
        {
            if (file == null)
                throw new InvalidOperationException($"Can't write {dir}/{stem}.json: {FileAccess.GetOpenError()}");
            file.StoreString(WeaponJson.Write(w));
        }
        thumbnail.SavePng($"{dir}/{stem}.png");
        return stem;
    }

    /// A centred square of image, resized to the thumbnail size.
    public static Image Square(Image image)
    {
        var side = Math.Min(image.GetWidth(), image.GetHeight());
        var square = image.GetRegion(new Rect2I((image.GetWidth() - side) / 2, (image.GetHeight() - side) / 2, side, side));
        square.Resize(ThumbnailSize, ThumbnailSize, Image.Interpolation.Lanczos);
        return square;
    }
}
