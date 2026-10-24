using System;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The materials column on the right: one round swatch per panel material (named by id, labelled with its name).
/// Pressing a swatch raises MaterialPressed, which starts dragging the material.
public partial class MaterialPanel : VBoxContainer
{
    public const int SwatchSize = 32;

    public event Action<string>? MaterialPressed;

    public void Fill(MaterialLibrary lib)
    {
        AddThemeConstantOverride("separation", 0);
        foreach (var m in lib.Visible)
        {
            var button = new Button
            {
                Name = m.Id, Text = m.Name, TooltipText = $"Drag onto a part to paint it {m.Name.ToLowerInvariant()} (Shift: the whole category)",
                Icon = Swatch(m), IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top,
                CustomMinimumSize = new Vector2(84, 0), Flat = true,
                FocusMode = FocusModeEnum.None, // no keyboard focus: Space/Enter mustn't start a paint
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            var id = m.Id;
            button.ButtonDown += () => MaterialPressed?.Invoke(id);
            AddChild(button);
        }
    }

    /// A filled circle of the material's colour, lit from the upper left so it reads as a ball; shinier materials
    /// get a brighter highlight.
    public static ImageTexture Swatch(MaterialDef m)
    {
        var image = Image.CreateEmpty(SwatchSize, SwatchSize, false, Image.Format.Rgba8);
        var baseColor = new Color(m.Color.X, m.Color.Y, m.Color.Z);
        var r = SwatchSize / 2f - 1f;
        var light = new Vector2(-0.4f, -0.45f);
        var shine = 1f - m.Roughness;
        for (var y = 0; y < SwatchSize; y++)
        for (var x = 0; x < SwatchSize; x++)
        {
            var p = new Vector2(x + 0.5f - SwatchSize / 2f, y + 0.5f - SwatchSize / 2f) / r;
            var d = p.Length();
            if (d > 1f)
                continue;
            var shade = Mathf.Lerp(1.15f, 0.55f, (p.Dot(-light.Normalized()) + 1f) / 2f);
            var highlight = Mathf.Max(0f, 1f - (p - light).Length() / 0.35f) * shine;
            var c = (baseColor * shade).Lerp(Colors.White, highlight * 0.8f);
            c.A = Mathf.Clamp((1f - d) * r, 0f, 1f); // soft edge
            image.SetPixel(x, y, c);
        }
        return ImageTexture.CreateFromImage(image);
    }
}
