using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Drives the builder with simulated mouse input and checks the weapon after each step. Not run by tools/test.sh
/// (it needs a window): `$GODOT --path . res://scenes/builder_smoke.tscn [-- --out <dir>]`. Exits with the number
/// of failed checks. With --out, saves screenshots mid-drag.
public partial class BuilderSmoke : Node
{
    BuilderScreen b = null!;
    int failures;
    string? outDir;

    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var i = Array.IndexOf(args, "--out");
        outDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        Input.UseAccumulatedInput = false;
        b = GD.Load<PackedScene>("res://scenes/builder.tscn").Instantiate<BuilderScreen>();
        AddChild(b);
        await Frames(5);
        try
        {
            await Run();
        }
        catch (Exception e)
        {
            Fail($"crashed: {e}");
        }
        GD.Print(failures == 0 ? "SMOKE: all checks passed" : $"SMOKE: {failures} check(s) failed");
        GetTree().Quit(failures);
    }

    async Task Run()
    {
        await Icons();
        MaterialsPanel();
        Check(b.RootHandle == "grip", "the shipped grip is the default handle");
        Check(b.Materials.Visible.Count == 7, $"the panel has seven materials ({b.Materials.Visible.Count})");
        Check(WeaponMaterials.Get("mithril") != WeaponMaterials.Get("steel"), "mithril is its own material, not the steel fallback");
        var root = b.Weapon.Root;
        await Palette("straight_blade", Socket(root, "top"));
        Check(At(root, "top") == "straight_blade", "blade attaches to the grip's top");
        await Palette("bar_guard", Socket(root, "guard"));
        Check(At(root, "guard") == "bar_guard", "guard attaches");
        await Palette("ball_pommel", Socket(root, "bottom"));
        Check(At(root, "bottom") == "ball_pommel", "pommel attaches");
        await Reframe();
        var blade = root.Children["top"];
        await Palette("axe_head", Socket(blade, "tip"));
        Check(At(blade, "tip") == "axe_head", "axe head attaches to the blade's tip");

        var before = Describe(b.Weapon.Root);
        await Mouse(Centre(blade), MouseButton.Left, true);
        await Mouse(Centre(blade), MouseButton.Left, false);
        Check(Describe(b.Weapon.Root) == before, "clicking a part without dragging leaves it there");

        await DragPart(Centre(root.Children["bottom"]), Socket(root, "top"));
        Check(Describe(b.Weapon.Root) == before, "a placed part dropped on a socket that won't take it goes back");

        await Palette("spike_pommel", Socket(root, "top"));
        Check(Describe(b.Weapon.Root) == before, "a pommel dropped on the top socket changes nothing");

        await DragPart(Centre(blade), new Vector2(1100, 600));
        Check(!root.Children.ContainsKey("top"), "dragging the blade off removes it");
        Check(!Contains(b.Weapon.Root, "axe_head"), "...with the axe on its tip");

        await Palette("straight_blade", Socket(root, "top"));
        await Palette("axe_head", Socket(root, "top"), shot: "swap.png");
        Check(At(root, "top") == "axe_head", "axe head swaps in for the blade");

        await Palette("spike_pommel", Socket(root, "bottom"), spin: true);
        Check(At(root, "bottom") == "spike_pommel" && root.Children["bottom"].RotationDegrees == 90f,
            "R spins a snapped part by the socket's step");

        await Mouse(await PaletteButton("ball_pommel"), MouseButton.Left, true);
        await Move(new Vector2(1100, 600));
        await Mouse(Socket(root, "bottom"), MouseButton.Left, false);
        await Frames(4);
        Check(At(root, "bottom") == "ball_pommel", "releasing over a socket attaches, even with no move there first");

        await Palette("long_haft", RootScreen());
        root = b.Weapon.Root;
        Check(root.PartId == "long_haft", "the root swaps to the long haft");
        Check(At(root, "top") == "axe_head" && !root.Children.ContainsKey("guard"), "...keeping the head, dropping the guard");
        Check(b.Status.Text.Contains("Bar Guard"), $"...and says the guard was dropped (\"{b.Status.Text}\")");

        await Reframe();
        before = Describe(b.Weapon.Root);
        await DragPart(Centre(root), new Vector2(1100, 600));
        Check(Describe(b.Weapon.Root) == before, "dragging the root does nothing");

        await EdgeCases();
        await Gallery();
        await Randomize();
        await Painting();

        if (outDir != null)
            await Shot("final.png");
    }

    async Task EdgeCases()
    {
        const string dir = "user://smoke_gallery";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        b.GalleryDir = dir;
        await Reframe();
        var root = b.Weapon.Root;
        var top = root.Children["top"];
        var before = Describe(root);

        // Clicks while the thumbnail is being taken are ignored.
        var build = b.Build("Capture Test");
        var at = Centre(top);
        Input.ParseInputEvent(new InputEventMouseButton
            { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        Input.ParseInputEvent(new InputEventMouseMotion
            { Position = at + new Vector2(100, 0), GlobalPosition = at + new Vector2(100, 0), ButtonMask = MouseButtonMask.Left });
        await build;
        Check(!b.Drag.Dragging && !b.Drag.Pressing, "pressing and dragging while a thumbnail is taken does nothing");
        await Mouse(at + new Vector2(100, 0), MouseButton.Left, false);
        Check(Describe(b.Weapon.Root) == before, "...and the weapon is unchanged");
        b.Weapon.Name = WeaponJson.DefaultName;
        await Reframe();

        // Zooming mid-drag re-snaps without a mouse move.
        var bottom = Socket(root, "bottom");
        await Mouse(await PaletteButton("ball_pommel"), MouseButton.Left, true);
        await Move(bottom + new Vector2(30, 0));
        Check(b.Drag.Snapped != null, "a part held near a socket snaps to it");
        for (var i = 0; i < 5; i++)
        {
            var mouse = bottom + new Vector2(30, 0);
            foreach (var pressed in new[] { true, false }) // a real wheel step sends both
                Input.ParseInputEvent(new InputEventMouseButton
                    { Position = mouse, GlobalPosition = mouse, ButtonIndex = MouseButton.WheelDown, Pressed = pressed });
            await Frames(1);
        }
        await Frames(2);
        Check(b.Drag.Snapped == null, "zooming out mid-drag un-snaps it without a mouse move");
        await Mouse(new Vector2(1100, 600), MouseButton.Left, false);
        await Reframe();
        Check(Describe(b.Weapon.Root) == before, "...and dropping it off the weapon changes nothing");

        // Losing focus mid-drag puts a picked-up part back.
        at = Centre(top);
        await Mouse(at, MouseButton.Left, true);
        await Move(at + new Vector2(10, 0));
        Check(b.Drag.Dragging, "moving a pressed part picks it up");
        b.Drag.Notification((int)Node.NotificationApplicationFocusOut);
        await Frames(2);
        Check(!b.Drag.Dragging && Describe(b.Weapon.Root) == before, "losing focus mid-drag puts the part back");
        await Mouse(at, MouseButton.Left, false);

        // Losing focus between pressing and dragging forgets the press.
        await Mouse(at, MouseButton.Left, true);
        b.Drag.Notification((int)Node.NotificationApplicationFocusOut);
        await Move(at + new Vector2(50, 0));
        Check(!b.Drag.Dragging, "losing focus after pressing a part forgets the press");
        await Mouse(at + new Vector2(50, 0), MouseButton.Left, false);
        Check(Describe(b.Weapon.Root) == before, "...and the weapon is unchanged");

        // Files that can't be loaded say why.
        using (FileAccess.Open($"{dir}/empty.json", FileAccess.ModeFlags.Write)) { }
        b.LoadFrom($"{dir}/empty.json");
        Check(b.Status.Text.Contains("is empty"), $"loading an empty file says so (\"{b.Status.Text}\")");
        b.LoadFrom("user://nope.json");
        Check(b.Status.Text.StartsWith("Couldn't open"), $"loading a missing file says so (\"{b.Status.Text}\")");
        Check(Describe(b.Weapon.Root) == before, "...and the weapon is unchanged");
        ClearDir(dir);
    }

    async Task Randomize()
    {
        const string dir = "user://smoke_gallery";
        b.GalleryDir = dir;
        var click = b.RandomizeButton.GetGlobalRect().GetCenter();
        await Mouse(click, MouseButton.Left, true);
        await Mouse(click, MouseButton.Left, false);
        Check(b.Status.Text.StartsWith("Seed "), $"Randomize shows its seed (\"{b.Status.Text}\")");
        Check(b.Weapon.Root.Children.ContainsKey("top"), "...and the random weapon has something on top");

        b.Randomize(42);
        var seeded = Describe(b.Weapon.Root);
        b.Randomize(42);
        Check(Describe(b.Weapon.Root) == seeded, $"the same seed gives the same weapon ({seeded})");

        await Reframe();
        var at = Centre(b.Weapon.Root.Children["top"]);
        await Mouse(at, MouseButton.Left, true);
        await Move(at + new Vector2(10, 0));
        Check(b.Drag.Dragging, "a random weapon's part can be picked up");
        b.Randomize(42);
        await Frames(2);
        Check(!b.Drag.Dragging && Describe(b.Weapon.Root) == seeded, "Randomize mid-drag drops the drag and the old part");
        await Mouse(at, MouseButton.Left, false);
        Check(Describe(b.Weapon.Root) == seeded, "...and letting go afterwards changes nothing");

        var stem = await b.Build("Random");
        Check(stem != null && stem.StartsWith("random-"), $"a random weapon can be built ({stem})");
        ClearDir(dir);
    }

    async Task Gallery()
    {
        const string dir = "user://smoke_gallery";
        ClearDir(dir);
        b.GalleryDir = dir;

        await Mouse(b.BuildButton.GetGlobalRect().GetCenter(), MouseButton.Left, true);
        await Mouse(b.BuildButton.GetGlobalRect().GetCenter(), MouseButton.Left, false);
        Check(b.NameDialog.Visible, "Build asks for a name");
        Check(b.NameEdit.Text == WeaponJson.DefaultName, "...starting from the weapon's name");
        b.NameEdit.Text = "Smoke Axe";
        var ok = b.NameDialog.Position + b.NameDialog.GetOkButton().GetGlobalRect().GetCenter();
        await Mouse(ok, MouseButton.Left, true);
        await Mouse(ok, MouseButton.Left, false);
        await Frames(10);
        Check(b.Status.Text.StartsWith("Saved smoke-axe-"), $"Save writes to the gallery and says so (\"{b.Status.Text}\")");
        Check(b.UiLayer.Visible, "...and the UI comes back after the thumbnail");

        var first = b.Status.Text["Saved ".Length..];
        var second = await b.Build("Smoke Axe");
        Check(first != second, $"saving the same name twice keeps both ({first}, {second})");
        Check(FileAccess.FileExists($"{dir}/{first}.json") && FileAccess.FileExists($"{dir}/{second}.json"), "...both JSON files exist");
        var png = Image.LoadFromFile($"{dir}/{second}.png");
        Check(png != null && png.GetWidth() == GallerySaver.ThumbnailSize && png.GetHeight() == GallerySaver.ThumbnailSize,
            "the thumbnail is 256 x 256");
        if (outDir != null)
            png?.SavePng($"{outDir}/thumbnail.png");

        var saved = Describe(b.Weapon.Root);
        b.NewWeapon();
        b.LoadFrom($"{dir}/{second}.json");
        await Frames(4);
        Check(Describe(b.Weapon.Root) == saved && b.Weapon.Name == "Smoke Axe", "loading a saved weapon gives the same weapon");
        using (var f = FileAccess.Open($"{dir}/stale.json", FileAccess.ModeFlags.Write))
            f.StoreString(FileAccess.GetFileAsString($"{dir}/{second}.json").Replace("\"axe_head\"", "\"melted_part\""));
        b.LoadFrom($"{dir}/stale.json");
        Check(b.Status.Text.Contains("melted_part"), $"loading says which parts were skipped (\"{b.Status.Text}\")");

        b.GalleryDir = "/System/not-writable";
        await b.Build("Nope");
        Check(b.Status.Text.StartsWith("Couldn't save"), $"a failed save says so (\"{b.Status.Text}\")");
        Check(b.UiLayer.Visible, "...and the UI comes back");
        ClearDir(dir);
    }

    async Task Painting()
    {
        const string dir = "user://smoke_gallery";
        ClearDir(dir);
        b.GalleryDir = dir;
        b.NewWeapon();
        var root = b.Weapon.Root;
        b.Editor.Attach(root, "top", "straight_blade");
        var blade = root.Children["top"];
        b.Editor.Attach(blade, "tip", "axe_head");
        b.Editor.Attach(root, "guard", "bar_guard");
        b.Editor.Attach(root, "bottom", "ball_pommel");
        b.Refresh();
        await Reframe();
        var axe = blade.Children["tip"];
        var guard = root.Children["guard"];
        var axeBefore = axe.Material;

        await Paint("gold", Centre(blade));
        Check(blade.Material == "gold" && axe.Material == axeBefore, "painting gold onto the blade paints just the blade");
        Check(b.Status.Text.Contains("Gold"), $"...and says so (\"{b.Status.Text}\")");

        var before = MaterialsOf(b.Weapon.Root);
        await Paint("wood", EmptySpot());
        Check(MaterialsOf(b.Weapon.Root) == before, "painting onto empty space changes nothing");

        await Paint("iron", Centre(root));
        Check(root.Material == "iron", "painting the grip (the root) paints it");

        await Paint("mithril", Centre(blade), shift: true);
        Check(blade.Material == "mithril" && axe.Material == "mithril" && guard.Material != "mithril",
            "Shift-painting the blade paints every blade and head, and nothing else");

        // The preview follows the cursor and leaves nothing behind.
        before = MaterialsOf(b.Weapon.Root);
        await Mouse(Swatch("obsidian"), MouseButton.Left, true);
        await Move(Centre(blade));
        Check(b.View.MaterialShown(blade) == WeaponMaterials.Get("obsidian"), "a swatch held over a part previews it");
        await Move(Centre(guard));
        Check(b.View.MaterialShown(blade) == WeaponMaterials.Get(blade.Material) && b.View.MaterialShown(guard) == WeaponMaterials.Get("obsidian"),
            "moving to another part moves the preview with it");
        await Move(EmptySpot());
        await Mouse(EmptySpot(), MouseButton.Left, false);
        Check(MaterialsOf(b.Weapon.Root) == before && b.View.MaterialShown(guard) == WeaponMaterials.Get(guard.Material),
            "...and letting go off the weapon changes nothing");

        // Interrupted paint drags leave the weapon as it was.
        await Mouse(Swatch("gold"), MouseButton.Left, true);
        await Move(Centre(blade));
        b.Drag.Notification((int)Node.NotificationApplicationFocusOut);
        await Frames(2);
        Check(b.Drag.Painting == null && b.View.MaterialShown(blade) == WeaponMaterials.Get(blade.Material),
            "losing focus mid-paint drops the paint and its preview");
        await Mouse(Centre(blade), MouseButton.Left, false);
        Check(MaterialsOf(b.Weapon.Root) == before, "...and letting go afterwards paints nothing");

        await Mouse(Swatch("gold"), MouseButton.Left, true);
        await Move(Centre(blade));
        await PressKey(Godot.Key.Escape);
        Check(b.Drag.Painting == null && b.View.MaterialShown(blade) == WeaponMaterials.Get(blade.Material),
            "Escape drops a paint drag and its preview");
        await Mouse(Centre(blade), MouseButton.Left, false);
        Check(MaterialsOf(b.Weapon.Root) == before, "...and letting go afterwards paints nothing");

        // Survives Build and Load.
        var stem = await b.Build("Painted");
        b.NewWeapon();
        b.LoadFrom($"{dir}/{stem}.json");
        await Frames(4);
        Check(MaterialsOf(b.Weapon.Root) == before, $"materials survive Build and Load ({before})");

        // Randomize mid-paint.
        b.Randomize(7);
        var clean = MaterialsOf(b.Weapon.Root);
        await Reframe();
        await Mouse(Swatch("gold"), MouseButton.Left, true);
        await Move(Centre(b.Weapon.Root));
        b.Randomize(7);
        await Frames(2);
        Check(b.Drag.Painting == null && MaterialsOf(b.Weapon.Root) == clean, "Randomize mid-paint drops the paint");
        await Mouse(Centre(b.Weapon.Root), MouseButton.Left, false);
        Check(MaterialsOf(b.Weapon.Root) == clean, "...and letting go afterwards paints nothing");

        if (outDir != null)
            await MaterialShots();

        // Part drags still work.
        b.NewWeapon();
        await Palette("ball_pommel", Socket(b.Weapon.Root, "bottom"));
        Check(At(b.Weapon.Root, "bottom") == "ball_pommel", "part drags still work after painting");
        ClearDir(dir);
    }

    /// A sword in each panel material, for judging how the materials look.
    async Task MaterialShots()
    {
        b.NewWeapon();
        b.Editor.Attach(b.Weapon.Root, "top", "broad_blade");
        b.Editor.Attach(b.Weapon.Root, "guard", "curved_guard");
        b.Editor.Attach(b.Weapon.Root, "bottom", "ball_pommel");
        foreach (var m in b.Materials.Visible)
        {
            foreach (var category in PartPalette.Tabs)
                b.Editor.SetMaterialForCategory(b.Weapon, category, m);
            b.Refresh();
            await Reframe();
            await Shot($"material-{m.Id}.png");
        }
    }

    static void ClearDir(string dir)
    {
        if (!DirAccess.DirExistsAbsolute(dir))
            return;
        foreach (var f in DirAccess.GetFilesAt(dir))
            DirAccess.RemoveAbsolute($"{dir}/{f}");
    }

    async Task Icons()
    {
        await b.IconsReady;
        var buttons = b.Palette.Buttons.ToList();
        Check(buttons.Count == b.Lib.All.Count(), $"the palette has a button per part ({buttons.Count})");
        Check(buttons.All(x => x.Icon != null && x.Icon.GetWidth() > 0 && x.Text == ""),
            "every palette button shows its icon instead of its name");

        // A corrupt cached icon is rendered again, not shown blank.
        var def = b.Lib.Get("straight_blade");
        var path = b.Icons.CachePath(def);
        DirAccess.MakeDirRecursiveAbsolute(PartIcons.CacheDir);
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Write))
            f.StoreString("junk");
        b.Icons.Forget();
        var icon = await b.Icons.Get(def);
        var onDisk = new Image();
        Check(icon.GetWidth() == PartIcons.Size && onDisk.Load(path) == Error.Ok && !onDisk.IsEmpty(),
            "a corrupt cached icon is rendered and saved again");
        await Frames(2);
        Check(DirAccess.GetFilesAt(PartIcons.CacheDir).Count(f => f.StartsWith("straight_blade-")) == 1,
            "...replacing the old file, so the cache keeps one icon per part");
        Check(Enumerable.Range(0, b.Palette.GetTabCount()).All(t => b.Palette.GetTabIcon(t) != null && b.Palette.GetTabTitle(t) == ""),
            "every palette tab shows an icon instead of its name");
        Check(b.Palette.Size.X <= PartPalette.Columns * (PartPalette.ButtonSize + 8) + 16, $"the palette stays narrow ({b.Palette.Size.X} px)");
        if (outDir != null)
        {
            await Shot("layout.png");
            b.Palette.CurrentTab = Array.IndexOf(PartPalette.Tabs, "Blades & Heads");
            await Frames(2);
            await Shot("palette-blades.png");
            b.Palette.CurrentTab = 0;
        }
    }

    void MaterialsPanel()
    {
        var ids = b.MaterialPanel.GetChildren().OfType<Button>().Select(x => x.Name.ToString()).ToList();
        Check(ids.SequenceEqual(["bronze", "iron", "steel", "gold", "mithril", "obsidian", "wood"]),
            $"the materials panel lists the seven materials in order ({string.Join(",", ids)})");
        var toolbar = b.BuildButton.GetParent<Control>().GetGlobalRect();
        var centre = GetViewport().GetVisibleRect().Size.X / 2f;
        Check(b.MaterialPanel.GetChildren().OfType<Button>().All(x => x.GetGlobalRect().Position.X > centre
                && !x.GetGlobalRect().Intersects(toolbar) && x.Icon != null),
            "every swatch has an icon, sits on the right and clears the toolbar");
        var screen = GetViewport().GetVisibleRect();
        Check(b.MaterialPanel.GetChildren().OfType<Button>().All(x => screen.Encloses(x.GetGlobalRect())),
            $"every swatch fits on a {screen.Size.X}x{screen.Size.Y} screen");
    }

    // --- steps ---

    async Task Palette(string partId, Vector2 to, bool spin = false, string? shot = null)
    {
        await Drag(await PaletteButton(partId), to, spin, shot);
    }

    /// Shows the part's tab and returns its button's centre.
    async Task<Vector2> PaletteButton(string partId)
    {
        var category = b.Lib.Get(partId).Category;
        b.Palette.CurrentTab = Array.IndexOf(PartPalette.Tabs, category);
        await Frames(2); // let the tab lay out
        return b.Palette.GetNode<Button>($"{category}/{partId}").GetGlobalRect().GetCenter();
    }

    async Task DragPart(Vector2 from, Vector2 to) => await Drag(from, to, false, null);

    async Task Drag(Vector2 from, Vector2 to, bool spin, string? shot)
    {
        await Mouse(from, MouseButton.Left, true);
        await Move((from + to) / 2f);
        await Move(to);
        if (spin)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, Pressed = true });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, Pressed = false });
            await Frames(2);
        }
        if (shot != null && outDir != null)
            await Shot(shot);
        await Mouse(to, MouseButton.Left, false);
        await Frames(4);
    }

    /// Drags a material swatch to a screen position and lets go (with Shift held, if shift).
    async Task Paint(string materialId, Vector2 to, bool shift = false)
    {
        var from = Swatch(materialId);
        await Mouse(from, MouseButton.Left, true);
        await Move((from + to) / 2f);
        await Move(to);
        await Mouse(to, MouseButton.Left, false, shift);
        await Frames(2);
    }

    async Task PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
        await Frames(2);
    }

    async Task Mouse(Vector2 at, MouseButton button, bool pressed, bool shift = false)
    {
        Input.WarpMouse(at);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : 0, ShiftPressed = shift,
        });
        await Frames(4);
    }

    async Task Move(Vector2 at)
    {
        Input.WarpMouse(at);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
        await Frames(2);
    }

    async Task Reframe()
    {
        b.Camera.Frame(b.View.Bounds);
        await Frames(4);
    }

    async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{outDir}/{name}");
    }

    // --- where things are on screen ---

    Vector2 Socket(WeaponPart owner, string name)
    {
        var s = b.View.Current!.Sockets.First(x => x.Owner == owner && x.Socket.Name == name);
        return b.Camera.UnprojectPosition(NumericsBridge.ToGodot(s.World).Origin);
    }

    Vector2 Swatch(string materialId) => b.MaterialPanel.GetNode<Button>(materialId).GetGlobalRect().GetCenter();

    /// A spot in the 3D view well away from the weapon and the panels.
    Vector2 EmptySpot() => GetViewport().GetVisibleRect().Size * new Vector2(0.7f, 0.9f);

    Vector2 RootScreen() => b.Camera.UnprojectPosition(Vector3.Zero);

    Vector2 Centre(WeaponPart part)
    {
        var p = b.View.Current!.Parts.First(x => x.Part == part);
        return b.Camera.UnprojectPosition(NumericsBridge.ToGodot(p.World) * PartMeshFactory.Get(p.Def).GetAabb().GetCenter());
    }

    // --- checks ---

    static string? At(WeaponPart owner, string socket) => owner.Children.TryGetValue(socket, out var c) ? c.PartId : null;

    static bool Contains(WeaponPart p, string partId) => p.PartId == partId || p.Children.Values.Any(c => Contains(c, partId));

    static string Describe(WeaponPart p) =>
        $"{p.PartId}@{p.RotationDegrees}({string.Join(",", p.Children.OrderBy(c => c.Key).Select(c => $"{c.Key}:{Describe(c.Value)}"))})";

    /// Each part's path in the tree with its material, e.g. "grip:iron top:gold top/tip:steel".
    static string MaterialsOf(WeaponPart p, string path = "") =>
        string.Join(" ", new[] { $"{(path == "" ? p.PartId : path)}:{p.Material}" }
            .Concat(p.Children.OrderBy(c => c.Key).Select(c => MaterialsOf(c.Value, path == "" ? c.Key : $"{path}/{c.Key}"))));

    void Check(bool ok, string what)
    {
        if (ok)
            GD.Print($"  ok   {what}");
        else
            Fail(what);
    }

    void Fail(string what)
    {
        failures++;
        GD.PrintErr($"  FAIL {what}  [{Describe(b.Weapon.Root)}]");
    }

    async Task Frames(int n)
    {
        for (var i = 0; i < n; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
