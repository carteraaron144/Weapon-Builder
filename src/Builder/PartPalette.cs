using System;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The tabbed part list on the left: one tab per category, one button per part (named by part id).
/// Pressing a button raises PartPressed, which starts a drag.
public partial class PartPalette : TabContainer
{
    public static readonly string[] Tabs = ["Handles", "Guards", "Pommels", "Blades & Heads"];

    public event Action<string>? PartPressed;

    public void Fill(PartLibrary lib)
    {
        ClipTabs = false; // every tab visible; the palette grows to fit them
        AddThemeFontSizeOverride("font_size", 14);
        foreach (var tab in Tabs)
        {
            var list = new VBoxContainer { Name = tab };
            foreach (var def in lib.ByCategory(tab))
            {
                var button = new Button { Name = def.Id, Text = def.Name, Alignment = HorizontalAlignment.Left };
                var id = def.Id;
                button.ButtonDown += () => PartPressed?.Invoke(id);
                list.AddChild(button);
            }
            AddChild(list);
        }
    }
}
