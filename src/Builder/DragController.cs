using System.Collections.Generic;
using System.Linq;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Dragging parts: from the palette onto sockets, and existing parts off the weapon or onto other sockets.
/// While dragging, compatible sockets show markers (pale blue = empty, amber = swap); the ghost snaps to the
/// nearest one within SnapPixels on screen, R spins it by the socket's step, and releasing attaches it there.
/// A part on the weapon is picked up once the cursor moves DragPixels with the button held (a click leaves it).
/// Released elsewhere over the weapon, a picked-up part goes back; released off the weapon, it's removed.
/// Handles target the root (a swap). Parts a swap leaves without a socket are named in the status line.
/// Painting: a material dragged from the materials panel previews on the part under the cursor (the root
/// included; with Shift, on every part of that part's category) and is applied there on release. Escape, or losing
/// focus, cancels any drag.
public partial class DragController : Node3D
{
    public const float SnapPixels = 48f;
    public const float DragPixels = 6f;
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
    (WeaponPart Part, Vector2 At)? pressed;                       // a part pressed but not yet dragged
    (WeaponPart Parent, string Socket, float Rotation)? origin;   // where a picked-up part came from
    string? painting;                                              // the material being dragged

    public bool Dragging => payload != null;
    /// True between pressing on a placed part and either dragging it or letting go.
    public bool Pressing => pressed != null;
    /// The material id being dragged from the materials panel, or null.
    public string? Painting => painting;
    public IReadOnlyList<Target> Targets => targets;
    public Target? Snapped => snapped;

    public void Setup(BuilderScreen builder) => screen = builder;

    /// Starts dragging a new part of kind partId.
    public void BeginNew(string partId)
    {
        var def = screen.Lib.Get(partId);
        Begin(new WeaponPart(partId, def.DefaultMaterial));
    }

    /// Starts dragging a material from the panel.
    public void BeginPaint(string materialId)
    {
        Cancel();
        screen.View.SetHovered(null);
        painting = materialId;
    }

    /// Picks an existing part up off the weapon (with its subtree). The root can't be picked up.
    public bool BeginExisting(WeaponPart part)
    {
        var parent = screen.Editor.FindParent(screen.Weapon, part, out var socket);
        var removed = screen.Editor.DetachPart(screen.Weapon, part);
        if (parent == null || removed == null)
            return false;
        screen.Refresh();
        Begin(removed);
        origin = (parent, socket, removed.RotationDegrees);
        return true;
    }

    void Begin(WeaponPart part)
    {
        Cancel();
        origin = null;
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
        pressed = null;
        snapped = null;
        painting = null;
        screen.View.ClearPreview();
        foreach (var t in targets)
            t.Marker.QueueFree();
        targets.Clear();
        ghost?.QueueFree();
        ghost = null;
    }

    /// Stops the drag as if it never started: a picked-up part goes back where it came from, a new one is dropped.
    public void Abort()
    {
        var part = payload;
        var from = origin;
        Cancel();
        if (part != null && from != null)
            PutBack(part, from.Value);
    }

    void PutBack(WeaponPart part, (WeaponPart Parent, string Socket, float Rotation) from)
    {
        part.RotationDegrees = from.Rotation;
        screen.Editor.Attach(from.Parent, from.Socket, part);
        screen.Refresh();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
            Abort();
    }

    public override void _Input(InputEvent e)
    {
        if (screen.Capturing)
            return;
        if (e is InputEventMouse m)
            mouse = m.Position;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && (Dragging || painting != null))
        {
            Abort();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (painting != null)
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } up)
                ReleasePaint(up.ShiftPressed || Input.IsKeyPressed(Key.Shift)); // not handled: the swatch gets its release
            return;
        }
        if (pressed is { } p && !Dragging)
        {
            if (e is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0
                && motion.Position.DistanceTo(p.At) >= DragPixels)
            {
                pressed = null;
                BeginExisting(p.Part);
            }
            else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            {
                pressed = null; // just a click
            }
        }
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
        if (Dragging || screen.Capturing || e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
            return;
        var part = screen.View.PartAt(press.Position, screen.Camera);
        if (part == null || part == screen.Weapon.Root)
            return;
        pressed = (part, press.Position);
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (painting != null)
        {
            var target = screen.View.PartAt(mouse, screen.Camera);
            screen.View.SetHovered(target);
            screen.View.Preview(PaintTargets(target, Input.IsKeyPressed(Key.Shift)), painting);
            return;
        }
        if (Dragging)
        {
            UpdateGhost(); // the camera may have moved (zoom, orbit) without the mouse moving
            return;
        }
        if (screen.Capturing)
            return;
        var overGui = GetViewport().GuiGetHoveredControl() != null;
        var part = overGui ? null : screen.View.PartAt(mouse, screen.Camera);
        screen.View.SetHovered(part == screen.Weapon.Root ? null : part);
    }

    /// The parts a paint would land on: the part under the cursor, or with Shift every part of its category, keeping
    /// only those that allow the material.
    List<WeaponPart> PaintTargets(WeaponPart? part, bool wholeCategory)
    {
        if (part == null || painting == null || !screen.Materials.TryGet(painting, out var material))
            return [];
        var category = screen.Lib.Get(part.PartId).Category;
        var parts = wholeCategory
            ? screen.View.Current!.Parts.Where(p => p.Def.Category == category).Select(p => p.Part)
            : [part];
        return parts.Where(p => Fitting.AllowsMaterial(screen.Lib.Get(p.PartId), material)).ToList();
    }

    void ReleasePaint(bool wholeCategory)
    {
        var id = painting!;
        var part = screen.View.PartAt(mouse, screen.Camera);
        Cancel();
        if (part == null || !screen.Materials.TryGet(id, out var material))
            return;
        var def = screen.Lib.Get(part.PartId);
        if (wholeCategory)
        {
            if (screen.Editor.SetMaterialForCategory(screen.Weapon, def.Category, material) > 0)
                screen.ShowStatus($"Painted every {def.Category} part {material.Name}");
        }
        else if (screen.Editor.SetMaterial(part, material))
            screen.ShowStatus($"Painted {def.Name} {material.Name}");
        screen.Refresh();
    }

    void Spin()
    {
        var step = snapped?.Socket?.Socket.RotationStepDegrees ?? 0f;
        if (payload == null || step <= 0f)
            return;
        payload.RotationDegrees = Fitting.SnapRotation(Fitting.SnapRotation(payload.RotationDegrees, step) + step, step);
        UpdateGhost();
    }

    void Release()
    {
        var part = payload!;
        var target = snapped;
        var from = origin;
        var overWeapon = OverWeapon(mouse);
        Cancel();
        if (target == null)
        {
            // Off the weapon: gone. Over it but not on a socket that takes it: a picked-up part goes back.
            if (from is { } o && overWeapon)
                PutBack(part, o);
            return;
        }
        var result = target.Socket == null
            ? screen.Editor.SwapRoot(screen.Weapon, part.PartId)
            : screen.Editor.Attach(target.Socket.Owner, target.Socket.Socket.Name, part);
        if (!result.Ok)
            return;
        screen.Refresh();
        if (result.Dropped.Count > 0)
            screen.ShowStatus("Dropped: " + string.Join(", ", result.Dropped.Select(d => screen.Lib.Get(d.PartId).Name)));
    }

    /// Whether a screen position is over the weapon (its projected bounds, padded by SnapPixels).
    bool OverWeapon(Vector2 at)
    {
        var cam = screen.Camera;
        var b = screen.View.Bounds;
        Rect2? rect = null;
        for (var i = 0; i < 8; i++)
        {
            var corner = b.GetEndpoint(i);
            if (cam.IsPositionBehind(corner))
                continue;
            var p = cam.UnprojectPosition(corner);
            rect = rect?.Expand(p) ?? new Rect2(p, Vector2.Zero);
        }
        return rect?.Grow(SnapPixels).HasPoint(at) ?? false;
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
            var spin = Fitting.SnapRotation(payload.RotationDegrees, s.Socket.RotationStepDegrees); // what Attach will store
            ghost.Transform = NumericsBridge.ToGodot(Layout.ChildWorld(s.Socket, def, spin, s.World));
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
