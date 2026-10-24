using System.Linq;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The builder: owns the weapon being built, the part library, the view of it, the palette and dragging.
/// Run with `-- --demo` to start with a sample sword.
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
    public Control Ui { get; private set; } = null!;

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

        var layer = new CanvasLayer();
        AddChild(layer);
        Ui = new Control { Name = "Ui", MouseFilter = Control.MouseFilterEnum.Ignore };
        Ui.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(Ui);
        Palette = new PartPalette { Name = "Palette", CustomMinimumSize = new Vector2(240, 0) };
        Palette.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        Palette.OffsetLeft = 12;
        Palette.OffsetTop = 12;
        Palette.OffsetBottom = -12;
        Ui.AddChild(Palette);
        Palette.Fill(Lib);
        Palette.PartPressed += Drag.BeginNew;

        NewWeapon();
        if (OS.GetCmdlineUserArgs().Contains("--demo"))
        {
            Editor.Attach(Weapon.Root, "top", "straight_blade");
            Editor.Attach(Weapon.Root, "guard", "bar_guard");
            Editor.Attach(Weapon.Root, "bottom", "ball_pommel");
            Refresh();
            Camera.Frame(View.Bounds);
        }
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
}
