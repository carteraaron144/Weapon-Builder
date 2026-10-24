using Godot;

namespace WeaponBuilder;

/// Orbits a pivot: right-drag to orbit (pitch kept within ±85°), wheel to zoom (0.4–6 m).
public partial class OrbitCamera : Camera3D
{
    [Export] public float Distance = 1.6f;
    [Export] public float YawDegrees = 30f;
    [Export] public float PitchDegrees = -12f;
    [Export] public float OrbitSpeed = 0.3f; // degrees per pixel

    public const float MinDistance = 0.4f, MaxDistance = 6f, MaxPitch = 85f;

    public Vector3 Pivot { get; set; }

    public override void _Ready() => Apply();

    /// Points at the centre of bounds from a distance that fits it.
    public void Frame(Aabb bounds)
    {
        Pivot = bounds.GetCenter();
        var radius = bounds.Size.Length() / 2f;
        Distance = Mathf.Clamp(radius / Mathf.Tan(Mathf.DegToRad(Fov / 2f)) * 1.1f, MinDistance, MaxDistance);
        Apply();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion m when (m.ButtonMask & MouseButtonMask.Right) != 0:
                YawDegrees -= m.Relative.X * OrbitSpeed;
                PitchDegrees = Mathf.Clamp(PitchDegrees - m.Relative.Y * OrbitSpeed, -MaxPitch, MaxPitch);
                Apply();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Distance = Mathf.Max(MinDistance, Distance * 0.9f);
                Apply();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Distance = Mathf.Min(MaxDistance, Distance / 0.9f);
                Apply();
                break;
        }
    }

    void Apply()
    {
        var rot = Basis.FromEuler(new Vector3(Mathf.DegToRad(PitchDegrees), Mathf.DegToRad(YawDegrees), 0));
        Transform = new Transform3D(rot, Pivot + rot * new Vector3(0, 0, Distance));
    }
}
