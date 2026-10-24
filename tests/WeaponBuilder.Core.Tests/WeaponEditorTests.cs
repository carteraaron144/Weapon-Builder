using WeaponBuilder.Core;
using static WeaponBuilder.Core.Tests.TestParts;

namespace WeaponBuilder.Core.Tests;

public class WeaponEditorTests
{
    static readonly PartLibrary Lib = new([
        Part("grip", ["handle"], 1f, 0.1f,
            Socket("top", ["blade", "head"]), Socket("guard", ["crossguard"]), Socket("bottom", ["pommel"])),
        Part("pike", ["handle"], 1f, 0.1f, Socket("top", ["blade", "head"])),
        Part("blade", ["blade"], 1f, 0.1f, Socket("tip", ["head"])),
        Part("blade2", ["blade"], 1f, 0.1f, Socket("tip", ["head"])),
        Part("axe", ["head"]),
        Part("bar", ["crossguard"]),
        Part("ball", ["pommel"]),
    ]);

    readonly WeaponEditor editor = new(Lib);
    readonly Weapon weapon = new("Test", new WeaponPart("grip", "steel"));

    WeaponPart Root => weapon.Root;

    [Fact]
    public void Attach_EmptyCompatibleSocket_Adds()
    {
        var r = editor.Attach(Root, "top", "blade");
        Assert.True(r.Ok);
        Assert.Equal("blade", Root.Children["top"].PartId);
        Assert.Equal("steel", Root.Children["top"].Material);
        Assert.Null(r.Replaced);
    }

    [Fact]
    public void Attach_IncompatibleSocket_RefusesAndChangesNothing()
    {
        Assert.False(editor.Attach(Root, "top", "ball").Ok);
        Assert.Empty(Root.Children);
    }

    [Fact]
    public void Attach_UnknownSocket_Refuses() => Assert.False(editor.Attach(Root, "nope", "blade").Ok);

    [Fact]
    public void Attach_UnknownPart_Refuses() => Assert.False(editor.Attach(Root, "top", "nope").Ok);

    [Fact]
    public void Attach_OccupiedSocket_ReplacesAndReturnsOld()
    {
        editor.Attach(Root, "top", "blade");
        var r = editor.Attach(Root, "top", "axe");
        Assert.True(r.Ok);
        Assert.Equal("blade", r.Replaced?.PartId);
        Assert.Equal("axe", Root.Children["top"].PartId);
    }

    [Fact]
    public void Attach_Swap_CarriesCompatibleChildren()
    {
        editor.Attach(Root, "top", "blade");
        editor.Attach(Root.Children["top"], "tip", "axe");
        var r = editor.Attach(Root, "top", "blade2");
        Assert.Equal("blade2", Root.Children["top"].PartId);
        Assert.Equal("axe", Root.Children["top"].Children["tip"].PartId);
        Assert.Empty(r.Dropped);
    }

    [Fact]
    public void Attach_Swap_DropsIncompatibleChildren()
    {
        editor.Attach(Root, "top", "blade");
        editor.Attach(Root.Children["top"], "tip", "axe");
        var r = editor.Attach(Root, "top", "axe");
        Assert.Empty(Root.Children["top"].Children);
        Assert.Equal("axe", Assert.Single(r.Dropped).PartId);
    }

    [Fact]
    public void Attach_ReattachSubtree_KeepsItsChildren()
    {
        var blade = new WeaponPart("blade", "iron");
        blade.Children["tip"] = new WeaponPart("axe", "steel");
        Assert.True(editor.Attach(Root, "top", blade).Ok);
        Assert.Same(blade, Root.Children["top"]);
        Assert.Equal("iron", Root.Children["top"].Material);
    }

    [Fact]
    public void SwapRoot_ToPike_KeepsTop_DropsGuardAndBottom()
    {
        editor.Attach(Root, "top", "blade");
        editor.Attach(Root, "guard", "bar");
        editor.Attach(Root, "bottom", "ball");
        var r = editor.SwapRoot(weapon, "pike");
        Assert.True(r.Ok);
        Assert.Equal("pike", weapon.Root.PartId);
        Assert.Equal("blade", weapon.Root.Children["top"].PartId);
        Assert.Equal(["ball", "bar"], r.Dropped.Select(p => p.PartId).Order());
    }

    [Fact]
    public void SwapRoot_NonHandle_Refuses()
    {
        Assert.False(editor.SwapRoot(weapon, "blade").Ok);
        Assert.Equal("grip", weapon.Root.PartId);
    }

    [Fact]
    public void DetachPart_Root_ReturnsNull_TreeUnchanged()
    {
        editor.Attach(Root, "top", "blade");
        Assert.Null(editor.DetachPart(weapon, weapon.Root));
        Assert.Equal("grip", weapon.Root.PartId);
        Assert.Single(weapon.Root.Children);
    }

    [Fact]
    public void Detach_Child_RemovesSubtree()
    {
        editor.Attach(Root, "top", "blade");
        editor.Attach(Root.Children["top"], "tip", "axe");
        var removed = editor.Detach(weapon, Root, "top");
        Assert.Equal("blade", removed?.PartId);
        Assert.Equal("axe", removed?.Children["tip"].PartId);
        Assert.Empty(Root.Children);
    }

    [Fact]
    public void FindParent_ReturnsOwnerAndSocket()
    {
        editor.Attach(Root, "top", "blade");
        editor.Attach(Root.Children["top"], "tip", "axe");
        var axe = Root.Children["top"].Children["tip"];
        Assert.Same(Root.Children["top"], editor.FindParent(weapon, axe, out var socket));
        Assert.Equal("tip", socket);
        Assert.Null(editor.FindParent(weapon, Root, out _));
    }

    [Fact]
    public void Attach_SnapsRotationToTheSocketStep()
    {
        var lib = new PartLibrary([Part("h", ["handle"], 1f, 0.1f, Socket("top", ["blade"], step: 120f)), Part("b", ["blade"])]);
        var root = new WeaponPart("h", "steel");
        var blade = new WeaponPart("b", "steel") { RotationDegrees = 90f };
        Assert.True(new WeaponEditor(lib).Attach(root, "top", blade).Ok);
        Assert.Equal(120f, blade.RotationDegrees);
    }

    [Fact]
    public void Attach_CarriedChildRotationSnapsToNewSocketStep()
    {
        var lib = new PartLibrary([
            Part("h", ["handle"], 1f, 0.1f, Socket("top", ["blade"])),
            Part("a", ["blade"], 1f, 0.1f, Socket("tip", ["head"], step: 90f)),
            Part("b", ["blade"], 1f, 0.1f, Socket("tip", ["head"], step: 120f)),
            Part("x", ["head"]),
        ]);
        var editor = new WeaponEditor(lib);
        var root = new WeaponPart("h", "steel");
        editor.Attach(root, "top", "a");
        var head = new WeaponPart("x", "steel") { RotationDegrees = 90f };
        editor.Attach(root.Children["top"], "tip", head);
        Assert.Equal(90f, head.RotationDegrees);
        editor.Attach(root, "top", "b");
        Assert.Same(head, root.Children["top"].Children["tip"]);
        Assert.Equal(120f, head.RotationDegrees);
    }

    [Fact]
    public void SetMaterial_PaintsThePart()
    {
        editor.Attach(Root, "top", "blade");
        Assert.True(editor.SetMaterial(Root.Children["top"], Mat("gold")));
        Assert.Equal("gold", Root.Children["top"].Material);
        Assert.Equal("steel", Root.Material);
    }

    static readonly PartLibrary CategoryLib = new([
        Part("h", ["handle"], 1f, 0.1f, Socket("a1", ["a"]), Socket("a2", ["a"]), Socket("b", ["b"])),
        Part("pa", ["a"]) with { Category = "A" },
        Part("pb", ["b"]) with { Category = "B" },
    ]);

    static Weapon CategoryWeapon(WeaponEditor e)
    {
        var w = new Weapon("T", new WeaponPart("h", "steel"));
        e.Attach(w.Root, "a1", "pa");
        e.Attach(w.Root, "a2", "pa");
        e.Attach(w.Root, "b", "pb");
        return w;
    }

    [Fact]
    public void SetMaterialForCategory_PaintsOnlyThatCategory()
    {
        var e = new WeaponEditor(CategoryLib);
        var w = CategoryWeapon(e);
        Assert.Equal(2, e.SetMaterialForCategory(w, "A", Mat("gold")));
        Assert.Equal("gold", w.Root.Children["a1"].Material);
        Assert.Equal("gold", w.Root.Children["a2"].Material);
        Assert.Equal("steel", w.Root.Children["b"].Material);
        Assert.Equal("steel", w.Root.Material);
    }

    [Fact]
    public void SetMaterialForCategory_IncludesTheRoot()
    {
        var e = new WeaponEditor(CategoryLib);
        var w = CategoryWeapon(e);
        Assert.Equal(1, e.SetMaterialForCategory(w, "Test", Mat("iron")));
        Assert.Equal("iron", w.Root.Material);
        Assert.Equal("steel", w.Root.Children["a1"].Material);
    }
}
