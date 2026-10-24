using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Drives the builder with simulated mouse input and checks the weapon after each step. Not run by tools/test.sh
/// (it needs a window): `$GODOT --path . res://scenes/builder_smoke.tscn [-- --out <dir>]`. Exits with the number
/// of failed checks. With --out, saves screenshots mid-drag.
public partial class BuilderSmoke : Node
{
    BuilderScreen b = null!;
    int failures;
    string? outDir;

    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var i = Array.IndexOf(args, "--out");
        outDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        Input.UseAccumulatedInput = false;
        b = GD.Load<PackedScene>("res://scenes/builder.tscn").Instantiate<BuilderScreen>();
        AddChild(b);
        await Frames(5);
        try
        {
            await Run();
        }
        catch (Exception e)
        {
            Fail($"crashed: {e}");
        }
        GD.Print(failures == 0 ? "SMOKE: all checks passed" : $"SMOKE: {failures} check(s) failed");
        GetTree().Quit(failures);
    }

    async Task Run()
    {
        var root = b.Weapon.Root;
        await Palette("straight_blade", Socket(root, "top"));
        Check(At(root, "top") == "straight_blade", "blade attaches to the grip's top");
        await Palette("bar_guard", Socket(root, "guard"));
        Check(At(root, "guard") == "bar_guard", "guard attaches");
        await Palette("ball_pommel", Socket(root, "bottom"));
        Check(At(root, "bottom") == "ball_pommel", "pommel attaches");
        await Reframe();
        var blade = root.Children["top"];
        await Palette("axe_head", Socket(blade, "tip"));
        Check(At(blade, "tip") == "axe_head", "axe head attaches to the blade's tip");

        var before = Describe(b.Weapon.Root);
        await Palette("spike_pommel", Socket(root, "top"));
        Check(Describe(b.Weapon.Root) == before, "a pommel dropped on the top socket changes nothing");

        await DragPart(Centre(blade), new Vector2(1100, 600));
        Check(!root.Children.ContainsKey("top"), "dragging the blade off removes it");
        Check(!Contains(b.Weapon.Root, "axe_head"), "...with the axe on its tip");

        await Palette("straight_blade", Socket(root, "top"));
        await Palette("axe_head", Socket(root, "top"), shot: "swap.png");
        Check(At(root, "top") == "axe_head", "axe head swaps in for the blade");

        await Palette("spike_pommel", Socket(root, "bottom"), spin: true);
        Check(At(root, "bottom") == "spike_pommel" && root.Children["bottom"].RotationDegrees == 90f,
            "R spins a snapped part by the socket's step");

        await Palette("long_haft", RootScreen());
        root = b.Weapon.Root;
        Check(root.PartId == "long_haft", "the root swaps to the long haft");
        Check(At(root, "top") == "axe_head" && !root.Children.ContainsKey("guard"), "...keeping the head, dropping the guard");

        await Reframe();
        before = Describe(b.Weapon.Root);
        await DragPart(Centre(root), new Vector2(1100, 600));
        Check(Describe(b.Weapon.Root) == before, "dragging the root does nothing");

        if (outDir != null)
            await Shot("final.png");
    }

    // --- steps ---

    async Task Palette(string partId, Vector2 to, bool spin = false, string? shot = null)
    {
        var category = b.Lib.Get(partId).Category;
        b.Palette.CurrentTab = Array.IndexOf(PartPalette.Tabs, category);
        await Frames(2);
        var button = b.Palette.GetNode<Button>($"{category}/{partId}");
        await Drag(button.GetGlobalRect().GetCenter(), to, spin, shot);
    }

    async Task DragPart(Vector2 from, Vector2 to) => await Drag(from, to, false, null);

    async Task Drag(Vector2 from, Vector2 to, bool spin, string? shot)
    {
        await Mouse(from, MouseButton.Left, true);
        await Move((from + to) / 2f);
        await Move(to);
        if (spin)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, Pressed = true });
            await Frames(2);
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.R, Pressed = false });
            await Frames(2);
        }
        if (shot != null && outDir != null)
            await Shot(shot);
        await Mouse(to, MouseButton.Left, false);
        await Frames(4);
    }

    async Task Mouse(Vector2 at, MouseButton button, bool pressed)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : 0,
        });
        await Frames(4);
    }

    async Task Move(Vector2 at)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
        await Frames(2);
    }

    async Task Reframe()
    {
        b.Camera.Frame(b.View.Bounds);
        await Frames(4);
    }

    async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"{outDir}/{name}");
    }

    // --- where things are on screen ---

    Vector2 Socket(WeaponPart owner, string name)
    {
        var s = b.View.Current!.Sockets.First(x => x.Owner == owner && x.Socket.Name == name);
        return b.Camera.UnprojectPosition(NumericsBridge.ToGodot(s.World).Origin);
    }

    Vector2 RootScreen() => b.Camera.UnprojectPosition(Vector3.Zero);

    Vector2 Centre(WeaponPart part)
    {
        var p = b.View.Current!.Parts.First(x => x.Part == part);
        return b.Camera.UnprojectPosition(NumericsBridge.ToGodot(p.World) * PartMeshFactory.Get(p.Def).GetAabb().GetCenter());
    }

    // --- checks ---

    static string? At(WeaponPart owner, string socket) => owner.Children.TryGetValue(socket, out var c) ? c.PartId : null;

    static bool Contains(WeaponPart p, string partId) => p.PartId == partId || p.Children.Values.Any(c => Contains(c, partId));

    static string Describe(WeaponPart p) =>
        $"{p.PartId}@{p.RotationDegrees}({string.Join(",", p.Children.OrderBy(c => c.Key).Select(c => $"{c.Key}:{Describe(c.Value)}"))})";

    void Check(bool ok, string what)
    {
        if (ok)
            GD.Print($"  ok   {what}");
        else
            Fail(what);
    }

    void Fail(string what)
    {
        failures++;
        GD.PrintErr($"  FAIL {what}  [{Describe(b.Weapon.Root)}]");
    }

    async Task Frames(int n)
    {
        for (var i = 0; i < n; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
