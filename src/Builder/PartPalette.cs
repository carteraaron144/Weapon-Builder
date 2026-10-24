using System;
using System.Collections.Generic;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The part palette on the left: one tab per category, each a grid of icon buttons (named by part id, with the
/// part name as tooltip). Buttons show the name until SetIcon gives them their icon; a tab shows its first part's
/// icon in place of its name, Spore-style, which keeps the palette narrow. Pressing a button raises PartPressed,
/// which starts a drag.
public partial class PartPalette : TabContainer
{
    public static readonly string[] Tabs = ["Handles", "Guards", "Pommels", "Blades & Heads"];
    public const int Columns = 3;
    public const int ButtonSize = 76;

    public event Action<string>? PartPressed;

    readonly Dictionary<string, Button> buttons = new();

    public void Fill(PartLibrary lib)
    {
        ClipTabs = false; // every tab visible; the palette grows to fit them
        AddThemeFontSizeOverride("font_size", 13);
        foreach (var tab in Tabs)
        {
            var grid = new GridContainer { Name = tab, Columns = Columns };
            foreach (var def in lib.ByCategory(tab))
            {
                var button = new Button
                {
                    Name = def.Id, Text = def.Name, TooltipText = def.Name,
                    CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
                    ExpandIcon = true, IconAlignment = HorizontalAlignment.Center,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipText = true,
                };
                button.AddThemeFontSizeOverride("font_size", 11);
                var id = def.Id;
                button.ButtonDown += () => PartPressed?.Invoke(id);
                grid.AddChild(button);
                buttons[id] = button;
            }
            AddChild(grid);
            SetTabTooltip(GetTabCount() - 1, tab);
        }
        AddThemeConstantOverride("icon_max_width", 34);
    }

    /// Swaps a button's text for its icon.
    public void SetIcon(string partId, Texture2D icon)
    {
        if (!buttons.TryGetValue(partId, out var button))
            return;
        button.Icon = icon;
        button.Text = "";
        var grid = (GridContainer)button.GetParent();
        if (grid.GetChild(0) == button)
        {
            var tab = grid.GetIndex();
            SetTabIcon(tab, icon);
            SetTabTitle(tab, "");
        }
    }

    public IEnumerable<Button> Buttons => buttons.Values;
}
