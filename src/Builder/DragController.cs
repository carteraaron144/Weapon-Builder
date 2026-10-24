using System.Collections.Generic;
using System.Linq;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Dragging parts: from the palette onto sockets, and existing parts off the weapon or onto other sockets.
/// While dragging, compatible sockets show markers (pale blue = empty, amber = swap); the ghost snaps to the
/// nearest one within SnapPixels on screen, R spins it by the socket's step, and releasing attaches it there.
/// Releasing anywhere else drops it (an existing part is then removed). Handles target the root (a swap).
public partial class DragController : Node3D
{
    public const float SnapPixels = 48f;
    static readonly Color EmptyColor = new(0.55f, 0.8f, 1f);
    static readonly Color SwapColor = new(1f, 0.7f, 0.2f);

    /// A place the dragged part can go: a socket, or the root (Socket == null).
    public sealed record Target(PlacedSocket? Socket, Node3D Marker);

    BuilderScreen screen = null!;
    WeaponPart? payload;     // what's being dragged (a new part, or a detached subtree)
    WeaponView? ghost;
    readonly List<Target> targets = new();
    Target? snapped;
    Vector2 mouse;

    public bool Dragging => payload != null;
    public IReadOnlyList<Target> Targets => targets;
    public Target? Snapped => snapped;

    public void Setup(BuilderScreen builder) => screen = builder;

    /// Starts dragging a new part of kind partId.
    public void BeginNew(string partId)
    {
        var def = screen.Lib.Get(partId);
        Begin(new WeaponPart(partId, def.DefaultMaterial));
    }

    /// Picks an existing part up off the weapon (with its subtree). The root can't be picked up.
    public bool BeginExisting(WeaponPart part)
    {
        var removed = screen.Editor.DetachPart(screen.Weapon, part);
        if (removed == null)
            return false;
        screen.Refresh();
        Begin(removed);
        return true;
    }

    void Begin(WeaponPart part)
    {
        Cancel();
        screen.View.SetHovered(null);
        payload = part;
        var def = screen.Lib.Get(part.PartId);
        AddChild(ghost = new WeaponView { IsGhost = true });
        ghost.Show(new Weapon("", part), screen.Lib);

        var markerSize = 0.012f * screen.Camera.Distance;
        if (def.Tags.Contains(WeaponEditor.HandleTag))
            targets.Add(new Target(null, AddMarker(Transform3D.Identity, SwapColor, markerSize)));
        else
            foreach (var s in screen.View.Current!.Sockets.Where(s => Fitting.Accepts(s.Socket, def)))
                targets.Add(new Target(s, AddMarker(NumericsBridge.ToGodot(s.World), s.Occupant == null ? EmptyColor : SwapColor, markerSize)));
        UpdateGhost();
    }

    Node3D AddMarker(Transform3D at, Color color, float radius)
    {
        var marker = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true,
            },
            Position = at.Origin,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(marker);
        return marker;
    }

    /// Drops whatever is being dragged without attaching it, and clears markers.
    public void Cancel()
    {
        payload = null;
        snapped = null;
        foreach (var t in targets)
            t.Marker.QueueFree();
        targets.Clear();
        ghost?.QueueFree();
        ghost = null;
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouse m)
            mouse = m.Position;
        if (!Dragging)
            return;
        switch (e)
        {
            case InputEventMouseMotion:
                UpdateGhost();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                UpdateGhost(); // snap where it was let go, not where it last moved
                Release(); // not marked handled, so the palette button that started it gets its release too
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R }:
                Spin();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (Dragging || e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
            return;
        var part = screen.View.PartAt(press.Position, screen.Camera);
        if (part != null && BeginExisting(part))
            GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (Dragging || screen.Capturing)
            return;
        var overGui = GetViewport().GuiGetHoveredControl() != null;
        var part = overGui ? null : screen.View.PartAt(mouse, screen.Camera);
        screen.View.SetHovered(part == screen.Weapon.Root ? null : part);
    }

    void Spin()
    {
        var step = snapped?.Socket?.Socket.RotationStepDegrees ?? 0f;
        if (payload == null || step <= 0f)
            return;
        payload.RotationDegrees = (payload.RotationDegrees + step) % 360f;
        UpdateGhost();
    }

    void Release()
    {
        var part = payload!;
        var target = snapped;
        Cancel();
        if (target == null)
            return; // dropped off the weapon
        var ok = target.Socket == null
            ? screen.Editor.SwapRoot(screen.Weapon, part.PartId).Ok
            : screen.Editor.Attach(target.Socket.Owner, target.Socket.Socket.Name, part).Ok;
        if (ok)
            screen.Refresh();
    }

    /// Snaps the ghost to the nearest target within SnapPixels, or floats it under the cursor.
    void UpdateGhost()
    {
        if (ghost == null || payload == null)
            return;
        var cam = screen.Camera;
        snapped = null;
        var best = SnapPixels;
        foreach (var t in targets)
        {
            var at = t.Marker.GlobalPosition;
            if (cam.IsPositionBehind(at))
                continue;
            var d = cam.UnprojectPosition(at).DistanceTo(mouse);
            if (d <= best)
            {
                best = d;
                snapped = t;
            }
        }
        foreach (var t in targets)
            t.Marker.Scale = Vector3.One * (t == snapped ? 1.6f : 1f);

        if (snapped?.Socket is { } s)
        {
            var def = screen.Lib.Get(payload.PartId);
            ghost.Transform = NumericsBridge.ToGodot(Layout.ChildWorld(s.Socket, def, payload.RotationDegrees, s.World));
        }
        else if (snapped != null)
        {
            ghost.Transform = Transform3D.Identity; // the root
        }
        else
        {
            var plane = new Plane(cam.GlobalBasis.Z, screen.View.Bounds.GetCenter());
            var hit = plane.IntersectsRay(cam.ProjectRayOrigin(mouse), cam.ProjectRayNormal(mouse));
            ghost.Transform = new Transform3D(Basis.Identity, hit ?? screen.View.Bounds.GetCenter());
        }
    }
}
