using Godot;

namespace WeaponBuilder;

/// Debug scene: every part in a row with its default material and name, for visual checks.
public partial class PartGallery : Node3D
{
    public override void _Ready()
    {
        var lib = PartFiles.LoadAll();
        var x = 0f;
        foreach (var def in lib.All)
        {
            AddChild(new MeshInstance3D { Mesh = PartMeshFactory.Get(def), Position = new Vector3(x, 0, 0) });
            AddChild(new Label3D
            {
                Text = def.Name, FontSize = 24, PixelSize = 0.002f, Position = new Vector3(x, -0.08f, 0),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            });
            x += 0.3f;
        }
        var centre = new Vector3((x - 0.3f) / 2f, 0.45f, 0);
        var cam = new Camera3D { Fov = 50 };
        AddChild(cam);
        cam.LookAtFromPosition(centre + new Vector3(0, 0.5f, 4.2f), centre);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0) });
        AddChild(StudioEnvironment.Make());
    }
}
