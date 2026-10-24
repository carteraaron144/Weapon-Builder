using System.Linq;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// The builder: owns the weapon being built, the part library and the view of it.
/// Run with `-- --demo` to start with a sample sword.
public partial class BuilderScreen : Node3D
{
    public const string DefaultHandle = "grip";

    public PartLibrary Lib { get; private set; } = null!;
    public WeaponEditor Editor { get; private set; } = null!;
    public Weapon Weapon { get; set; } = null!;
    public WeaponView View { get; private set; } = null!;
    public OrbitCamera Camera { get; private set; } = null!;

    public override void _Ready()
    {
        Lib = PartFiles.LoadAll();
        Editor = new WeaponEditor(Lib);
        AddChild(StudioEnvironment.Make());
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, 35, 0) });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, -150, 0), LightEnergy = 0.35f });
        AddChild(View = new WeaponView());
        AddChild(Camera = new OrbitCamera { Fov = 50 });

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
        Weapon = new Weapon(WeaponJson.DefaultName, new WeaponPart(DefaultHandle, Lib.Get(DefaultHandle).DefaultMaterial));
        Refresh();
        Camera.Frame(View.Bounds);
    }

    /// Redraws the weapon after an edit.
    public void Refresh() => View.Show(Weapon, Lib);
}
