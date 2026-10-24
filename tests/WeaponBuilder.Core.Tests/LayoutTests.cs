using System.Numerics;
using WeaponBuilder.Core;
using static WeaponBuilder.Core.Tests.TestParts;

namespace WeaponBuilder.Core.Tests;

public class LayoutTests
{
    static readonly PartLibrary Lib = new([
        Part("grip", ["handle"], 1f, 0.1f,
            Socket("top", ["blade"], 0.5f, 0.05f, position: new Vector3(0, 1, 0)),
            Socket("side", ["blade"], 1f, 0.1f, rotation: new Vector3(0, 0, 90)),
            Socket("bottom", ["pommel"])),
        Part("blade", ["blade"], 1f, 0.1f, Socket("tip", ["head"], 0.25f, 0.025f, position: new Vector3(0, 1, 0))),
        Part("axe", ["head"], 1f, 0.1f),
    ]);

    static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"expected {expected}, got {actual}");
    }

    static Weapon Build(out WeaponPart blade, out WeaponPart axe)
    {
        var root = new WeaponPart("grip", "steel");
        blade = new WeaponPart("blade", "steel");
        axe = new WeaponPart("axe", "steel");
        blade.Children["tip"] = axe;
        root.Children["top"] = blade;
        return new Weapon("W", root);
    }

    static PlacedPart Placed(LayoutResult r, WeaponPart p) => r.Parts.Single(x => ReferenceEquals(x.Part, p));

    [Fact]
    public void Root_IsIdentity()
    {
        var w = Build(out _, out _);
        Assert.Equal(Matrix4x4.Identity, Placed(Layout.Compute(w, Lib), w.Root).World);
    }

    [Fact]
    public void Child_SitsAtSocketPosition()
    {
        var w = Build(out var blade, out _);
        var placed = Placed(Layout.Compute(w, Lib), blade);
        Near(new Vector3(0, 1, 0), placed.World.Translation);
        Assert.Same(w.Root, placed.Parent);
        Assert.Equal("top", placed.Socket);
    }

    [Fact]
    public void Child_ScaleInheritedByGrandchild()
    {
        // blade fits top at 0.5; axe fits tip at 0.25 of the blade's size → 0.125 overall.
        var w = Build(out _, out var axe);
        var world = Placed(Layout.Compute(w, Lib), axe).World;
        Near(new Vector3(0.125f, 0, 0), Vector3.TransformNormal(Vector3.UnitX, world));
        Near(new Vector3(0, 1.5f, 0), world.Translation); // tip is 1 m up the blade, which is half size
    }

    [Fact]
    public void SocketRotation_Applied()
    {
        var w = Build(out _, out _);
        var side = new WeaponPart("blade", "steel");
        w.Root.Children["side"] = side;
        Near(-Vector3.UnitX, Vector3.TransformNormal(Vector3.UnitY, Placed(Layout.Compute(w, Lib), side).World));
    }

    [Fact]
    public void PartRotation_SpinsAboutSocketAxis()
    {
        var w = Build(out _, out _);
        var side = new WeaponPart("blade", "steel") { RotationDegrees = 90 };
        w.Root.Children["bottom"] = side; // identity socket, scale 1
        var world = Placed(Layout.Compute(w, Lib), side).World;
        Near(-Vector3.UnitZ, Vector3.TransformNormal(Vector3.UnitX, world));
        Near(Vector3.UnitY, Vector3.TransformNormal(Vector3.UnitY, world));
    }

    [Fact]
    public void Sockets_IncludeOccupiedAndEmpty()
    {
        var w = Build(out var blade, out var axe);
        var sockets = Layout.Compute(w, Lib).Sockets;
        Assert.Equal(4, sockets.Count); // grip: top, side, bottom; blade: tip
        Assert.Same(blade, sockets.Single(s => s.Socket.Name == "top").Occupant);
        Assert.Null(sockets.Single(s => s.Socket.Name == "bottom").Occupant);
        var tip = sockets.Single(s => s.Socket.Name == "tip");
        Assert.Same(blade, tip.Owner);
        Assert.Same(axe, tip.Occupant);
        Near(new Vector3(0, 1.5f, 0), tip.World.Translation);
    }
}
