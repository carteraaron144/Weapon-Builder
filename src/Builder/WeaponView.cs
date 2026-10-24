using System.Collections.Generic;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Shows a weapon: one node per part, placed by Layout, each with an outline (shown on hover)
/// and a box collider on PickLayer for picking parts under the cursor.
public partial class WeaponView : Node3D
{
    public const uint PickLayer = 1 << 1;
    public static readonly Color HoverColor = new(1f, 0.84f, 0.48f);

    readonly Dictionary<ulong, WeaponPart> partOfBody = new();
    readonly Dictionary<WeaponPart, MeshInstance3D> outlineOf = new();
    readonly Dictionary<WeaponPart, MeshInstance3D> meshOf = new();
    readonly HashSet<WeaponPart> previewed = new();
    WeaponPart? hovered;

    /// A ghost is see-through and can't be picked (drag previews).
    public bool IsGhost { get; init; }

    public LayoutResult? Current { get; private set; }
    public Aabb Bounds { get; private set; }

    /// Rebuilds the view from the weapon.
    public void Show(Weapon w, PartLibrary lib)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        partOfBody.Clear();
        outlineOf.Clear();
        meshOf.Clear();
        previewed.Clear();
        hovered = null;

        Current = Layout.Compute(w, lib);
        var bounds = new Aabb();
        var first = true;
        foreach (var placed in Current.Parts)
        {
            var mesh = PartMeshFactory.Get(placed.Def);
            var node = MakePartNode(mesh, IsGhost ? WeaponMaterials.Ghost(placed.Part.Material) : WeaponMaterials.Get(placed.Part.Material));
            node.Transform = NumericsBridge.ToGodot(placed.World);
            AddChild(node);

            var aabb = mesh.GetAabb();
            var worldAabb = node.Transform * aabb;
            bounds = first ? worldAabb : bounds.Merge(worldAabb);
            first = false;
            if (IsGhost)
                continue;
            var body = new StaticBody3D { CollisionLayer = PickLayer, CollisionMask = 0 };
            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = aabb.Size.Max(Vector3.One * 0.01f) },
                Position = aabb.GetCenter(),
            });
            node.AddChild(body);
            partOfBody[body.GetInstanceId()] = placed.Part;
            outlineOf[placed.Part] = node.GetNode<MeshInstance3D>("Outline");
            meshOf[placed.Part] = node.GetNode<MeshInstance3D>("Mesh");
        }
        Bounds = bounds;
    }

    /// A part's mesh with a hidden outline child named "Outline". Also used for drag ghosts.
    public static Node3D MakePartNode(Mesh mesh, Material material)
    {
        var node = new Node3D();
        node.AddChild(new MeshInstance3D { Name = "Mesh", Mesh = mesh, MaterialOverride = material });
        var outline = new MeshInstance3D
        {
            Name = "Outline", Mesh = OutlineShell.For(mesh), MaterialOverride = OutlineShell.Material(HoverColor),
            Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        node.AddChild(outline);
        return node;
    }

    /// The part under a screen position, or null.
    public WeaponPart? PartAt(Vector2 screenPos, Camera3D cam)
    {
        var from = cam.ProjectRayOrigin(screenPos);
        var query = PhysicsRayQueryParameters3D.Create(from, from + cam.ProjectRayNormal(screenPos) * 100f, PickLayer);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
            return null;
        var collider = hit["collider"].AsGodotObject();
        return collider != null && partOfBody.TryGetValue(collider.GetInstanceId(), out var part) ? part : null;
    }

    /// Shows these parts in another material (a paint preview), putting back any earlier preview first.
    public void Preview(IEnumerable<WeaponPart> parts, string? materialId)
    {
        ClearPreview();
        if (materialId == null)
            return;
        foreach (var part in parts)
            if (meshOf.TryGetValue(part, out var mesh))
            {
                mesh.MaterialOverride = WeaponMaterials.Get(materialId);
                previewed.Add(part);
            }
    }

    /// Shows every previewed part in its own material again.
    public void ClearPreview()
    {
        foreach (var part in previewed)
            if (meshOf.TryGetValue(part, out var mesh))
                mesh.MaterialOverride = WeaponMaterials.Get(part.Material);
        previewed.Clear();
    }

    /// The material a part is drawn with right now (its own, or a preview).
    public Material? MaterialShown(WeaponPart part) => meshOf.TryGetValue(part, out var mesh) ? mesh.MaterialOverride : null;

    /// Outlines one part (or none).
    public void SetHovered(WeaponPart? part)
    {
        if (part == hovered)
            return;
        if (hovered != null && outlineOf.TryGetValue(hovered, out var old))
            old.Visible = false;
        hovered = part;
        if (part != null && outlineOf.TryGetValue(part, out var now))
            now.Visible = true;
    }
}
