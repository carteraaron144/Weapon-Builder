using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The builder: owns the weapon being built, the part library, the view of it, the palette, dragging and the
/// New/Randomize/Build toolbar. Run with `-- --demo` to start with a sample sword, `-- --seed <n>` for a random
/// weapon, or `-- --load <path>` to open a saved weapon.
public partial class BuilderScreen : Node3D
{
    public const string DefaultHandle = "grip";

    /// What a new weapon is framed on: room for a typical weapon above the handle, so new parts stay in view.
    public static readonly Aabb NewWeaponFrame = new(new Vector3(-0.3f, -0.2f, -0.3f), new Vector3(0.6f, 1.4f, 0.6f));

    public PartLibrary Lib { get; private set; } = null!;
    public MaterialLibrary Materials => WeaponMaterials.Library;
    /// The handle new weapons start from: DefaultHandle, or another handle if that one is missing.
    public string RootHandle { get; private set; } = DefaultHandle;
    public WeaponEditor Editor { get; private set; } = null!;
    public Weapon Weapon { get; set; } = null!;
    public WeaponView View { get; private set; } = null!;
    public OrbitCamera Camera { get; private set; } = null!;
    public PartPalette Palette { get; private set; } = null!;
    public PartIcons Icons { get; private set; } = null!;
    public MaterialPanel MaterialPanel { get; private set; } = null!;
    Control materialsBox = null!, toolbar = null!;
    /// Finishes when every palette button has its icon.
    public Task IconsReady { get; private set; } = Task.CompletedTask;
    public DragController Drag { get; private set; } = null!;
    public CanvasLayer UiLayer { get; private set; } = null!;
    public Button BuildButton { get; private set; } = null!;
    public Button NewButton { get; private set; } = null!;
    public Button RandomizeButton { get; private set; } = null!;
    public ConfirmationDialog NameDialog { get; private set; } = null!;
    public LineEdit NameEdit { get; private set; } = null!;
    public Label Status { get; private set; } = null!;
    /// True while a thumbnail is being taken (no hover outline).
    public bool Capturing { get; private set; }
    /// Where Build saves (the smoke test points it elsewhere).
    public string GalleryDir { get; set; } = GallerySaver.DefaultDir;

    public override void _Ready()
    {
        Lib = PartFiles.LoadAll();
        Editor = new WeaponEditor(Lib);
        AddChild(StudioEnvironment.Make());
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, 35, 0) });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, -150, 0), LightEnergy = 0.35f });
        AddChild(View = new WeaponView());
        AddChild(Camera = new OrbitCamera { Fov = 50 });
        AddChild(Drag = new DragController());
        Drag.Setup(this);
        AddChild(Icons = new PartIcons { Name = "Icons" });
        BuildUi();
        IconsReady = FillIcons();

        if (Lib.PickHandle(DefaultHandle) is not { } handle)
        {
            GD.PushError("No handle parts in data/parts");
            ShowStatus("No handle parts in data/parts", sticky: true);
            Drag.ProcessMode = ProcessModeEnum.Disabled;
            return;
        }
        if (handle != DefaultHandle)
            GD.PushWarning($"Default handle '{DefaultHandle}' is missing; using '{handle}'");
        RootHandle = handle;

        NewWeapon();
        var args = OS.GetCmdlineUserArgs();
        var load = Array.IndexOf(args, "--load");
        var seedAt = Array.IndexOf(args, "--seed");
        if (load >= 0 && load + 1 < args.Length)
        {
            LoadFrom(args[load + 1]);
        }
        else if (seedAt >= 0 && seedAt + 1 < args.Length && int.TryParse(args[seedAt + 1], out var seed))
        {
            Randomize(seed);
        }
        else if (args.Contains("--demo"))
        {
            Editor.Attach(Weapon.Root, "top", "straight_blade");
            Editor.Attach(Weapon.Root, "guard", "bar_guard");
            Editor.Attach(Weapon.Root, "bottom", "ball_pommel");
            Refresh();
            Camera.Frame(View.Bounds);
        }
    }

    void BuildUi()
    {
        AddChild(UiLayer = new CanvasLayer());
        var ui = new Control { Name = "Ui", MouseFilter = Control.MouseFilterEnum.Ignore };
        ui.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UiLayer.AddChild(ui);

        Palette = new PartPalette { Name = "Palette" };
        Palette.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        Palette.OffsetLeft = 12;
        Palette.OffsetTop = 12;
        Palette.OffsetBottom = -12;
        ui.AddChild(Palette);
        Palette.Fill(Lib);
        Palette.PartPressed += Drag.BeginNew;

        materialsBox = new PanelContainer { Name = "Materials" };
        materialsBox.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        materialsBox.GrowHorizontal = Control.GrowDirection.Begin;
        materialsBox.OffsetRight = -12;
        materialsBox.OffsetTop = 56;
        materialsBox.OffsetBottom = -12;
        materialsBox.AddChild(MaterialPanel = new MaterialPanel { Name = "Swatches" });
        ui.AddChild(materialsBox);
        MaterialPanel.Fill(Materials);
        MaterialPanel.MaterialPressed += Drag.BeginPaint;

        toolbar = new HBoxContainer { Name = "Toolbar" };
        toolbar.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        toolbar.GrowHorizontal = Control.GrowDirection.Begin;
        toolbar.OffsetRight = -12;
        toolbar.OffsetTop = 12;
        toolbar.AddChild(NewButton = new Button { Name = "New", Text = "New" });
        toolbar.AddChild(RandomizeButton = new Button { Name = "Randomize", Text = "Randomize" });
        toolbar.AddChild(BuildButton = new Button { Name = "Build", Text = "Build" });
        ui.AddChild(toolbar);
        NewButton.Pressed += NewWeapon;
        RandomizeButton.Pressed += () => Randomize();
        BuildButton.Pressed += AskName;

        Status = new Label { Name = "Status", HorizontalAlignment = HorizontalAlignment.Center };
        Status.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        Status.OffsetTop = 16;
        ui.AddChild(Status);

        NameDialog = new ConfirmationDialog { Title = "Build weapon", OkButtonText = "Save", MinSize = new Vector2I(320, 0) };
        NameDialog.AddChild(NameEdit = new LineEdit { PlaceholderText = "Weapon name" });
        NameDialog.RegisterTextEnter(NameEdit);
        NameDialog.Confirmed += async () => await Build(NameEdit.Text);
        AddChild(NameDialog);
    }

    /// Gives every palette button its icon, tab by tab.
    async Task FillIcons()
    {
        foreach (var tab in PartPalette.Tabs)
            foreach (var def in Lib.ByCategory(tab))
                Palette.SetIcon(def.Id, await Icons.Get(def));
    }

    /// Whether a screen position is over the palette, the materials panel or the toolbar (not the 3D view).
    public bool OverUi(Vector2 at) =>
        Palette.GetGlobalRect().HasPoint(at) || materialsBox.GetGlobalRect().HasPoint(at) || toolbar.GetGlobalRect().HasPoint(at);

    /// Starts over with a bare handle.
    public void NewWeapon()
    {
        Drag?.Cancel();
        Weapon = new Weapon(WeaponJson.DefaultName, new WeaponPart(RootHandle, Lib.Get(RootHandle).DefaultMaterial));
        Refresh();
        Camera.Frame(NewWeaponFrame);
    }

    /// Replaces the weapon with a random one and shows the seed. Returns the seed used.
    public int Randomize(int? seed = null)
    {
        Drag.Cancel();
        var used = seed ?? System.Random.Shared.Next();
        Weapon = RandomWeapon.Build(Lib, Materials, new System.Random(used));
        Refresh();
        Camera.Frame(View.Bounds);
        ShowStatus($"Seed {used}");
        return used;
    }

    /// Redraws the weapon after an edit.
    public void Refresh() => View.Show(Weapon, Lib);

    /// Opens a saved weapon (parts that no longer exist or fit are dropped, with a warning each).
    public void LoadFrom(string path)
    {
        var file = path.GetFile();
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null)
        {
            var error = FileAccess.GetOpenError();
            GD.PushError($"Can't read {path}: {error}");
            ShowStatus($"Couldn't open {file}: {error}");
            return;
        }
        var json = f.GetAsText();
        if (json.Trim() == "")
        {
            ShowStatus($"{file} is empty");
            return;
        }
        Drag.Cancel();
        Weapon = WeaponJson.Read(json, Lib, Materials, RootHandle, out var warnings);
        foreach (var w in warnings)
            GD.PushWarning($"{path}: {w}");
        Refresh();
        Camera.Frame(View.Bounds);
        if (warnings.Count > 0)
            ShowStatus("Loaded with changes: " + string.Join("; ", warnings));
    }

    void AskName()
    {
        Drag.Cancel();
        NameEdit.Text = Weapon.Name;
        NameDialog.PopupCentered();
        NameEdit.GrabFocus();
        NameEdit.SelectAll();
    }

    /// Names the weapon and saves it with a thumbnail to the gallery. Returns the saved file stem, or null
    /// if saving failed (the status line says why).
    public async Task<string?> Build(string name)
    {
        Weapon.Name = string.IsNullOrWhiteSpace(name) ? WeaponJson.DefaultName : name.Trim();
        try
        {
            var thumbnail = await CaptureThumbnail();
            var stem = GallerySaver.Save(Weapon, thumbnail, GalleryDir);
            ShowStatus($"Saved {stem}");
            return stem;
        }
        catch (Exception e)
        {
            GD.PushError($"Couldn't save {Weapon.Name}: {e}");
            ShowStatus($"Couldn't save: {e.Message}");
            return null;
        }
    }

    /// The weapon alone (no UI, markers or outline), framed, as a square thumbnail. The camera goes back after.
    async Task<Image> CaptureThumbnail()
    {
        var camera = Camera.State;
        Capturing = true;
        UiLayer.Visible = false;
        View.SetHovered(null);
        Camera.Frame(View.Bounds);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GallerySaver.Square(GetViewport().GetTexture().GetImage());
        UiLayer.Visible = true;
        Camera.State = camera;
        Capturing = false;
        return image;
    }

    /// Shows a line at the top of the screen for a few seconds (or until replaced, if sticky).
    public void ShowStatus(string text, bool sticky = false)
    {
        Status.Text = text;
        if (sticky)
            return;
        GetTree().CreateTimer(3).Timeout += () =>
        {
            if (Status.Text == text)
                Status.Text = "";
        };
    }
}
