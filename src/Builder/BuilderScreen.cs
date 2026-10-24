using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The builder: owns the weapon being built, the part library, the view of it, the palette, dragging and the
/// Build/New toolbar. Run with `-- --demo` to start with a sample sword, or `-- --load <path>` to open a saved weapon.
public partial class BuilderScreen : Node3D
{
    public const string DefaultHandle = "grip";

    /// What a new weapon is framed on: room for a typical weapon above the handle, so new parts stay in view.
    public static readonly Aabb NewWeaponFrame = new(new Vector3(-0.3f, -0.2f, -0.3f), new Vector3(0.6f, 1.4f, 0.6f));

    public PartLibrary Lib { get; private set; } = null!;
    public WeaponEditor Editor { get; private set; } = null!;
    public Weapon Weapon { get; set; } = null!;
    public WeaponView View { get; private set; } = null!;
    public OrbitCamera Camera { get; private set; } = null!;
    public PartPalette Palette { get; private set; } = null!;
    public DragController Drag { get; private set; } = null!;
    public CanvasLayer UiLayer { get; private set; } = null!;
    public Button BuildButton { get; private set; } = null!;
    public Button NewButton { get; private set; } = null!;
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
        BuildUi();

        NewWeapon();
        var args = OS.GetCmdlineUserArgs();
        var load = Array.IndexOf(args, "--load");
        if (load >= 0 && load + 1 < args.Length)
        {
            LoadFrom(args[load + 1]);
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

        Palette = new PartPalette { Name = "Palette", CustomMinimumSize = new Vector2(240, 0) };
        Palette.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        Palette.OffsetLeft = 12;
        Palette.OffsetTop = 12;
        Palette.OffsetBottom = -12;
        ui.AddChild(Palette);
        Palette.Fill(Lib);
        Palette.PartPressed += Drag.BeginNew;

        var toolbar = new HBoxContainer { Name = "Toolbar" };
        toolbar.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        toolbar.GrowHorizontal = Control.GrowDirection.Begin;
        toolbar.OffsetRight = -12;
        toolbar.OffsetTop = 12;
        toolbar.AddChild(NewButton = new Button { Name = "New", Text = "New" });
        toolbar.AddChild(BuildButton = new Button { Name = "Build", Text = "Build" });
        ui.AddChild(toolbar);
        NewButton.Pressed += NewWeapon;
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

    /// Starts over with a bare handle.
    public void NewWeapon()
    {
        Drag?.Cancel();
        Weapon = new Weapon(WeaponJson.DefaultName, new WeaponPart(DefaultHandle, Lib.Get(DefaultHandle).DefaultMaterial));
        Refresh();
        Camera.Frame(NewWeaponFrame);
    }

    /// Redraws the weapon after an edit.
    public void Refresh() => View.Show(Weapon, Lib);

    /// Opens a saved weapon (parts that no longer exist or fit are dropped, with a warning each).
    public void LoadFrom(string path)
    {
        var json = FileAccess.GetFileAsString(path);
        if (json == "")
        {
            GD.PushError($"Can't read {path}: {FileAccess.GetOpenError()}");
            return;
        }
        Drag.Cancel();
        Weapon = WeaponJson.Read(json, Lib, DefaultHandle, out var warnings);
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

    /// Shows a line at the top of the screen for a few seconds.
    public void ShowStatus(string text)
    {
        Status.Text = text;
        GetTree().CreateTimer(3).Timeout += () =>
        {
            if (Status.Text == text)
                Status.Text = "";
        };
    }
}
