using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using WeaponBuilder.Core;

namespace WeaponBuilder;

/// Renders part icons for the palette: a small three-quarter view on a transparent background, lit by a key and a
/// fill light (ported from The Mountain's tools/render_icons.gd). Icons are cached in user://icons under a name that
/// changes when the part, the materials or the icon style change, so stale icons are never shown.
public partial class PartIcons : Node
{
    /// The finished icon's size in pixels (it's rendered at 4x and scaled down, for clean edges).
    public const int Size = 72;
    public const string CacheDir = "user://icons";
    const string StyleVersion = "v3 yaw30 elev20 roll45 studiosky";
    const float Yaw = 30f, Elevation = 20f, LongRoll = -45f;

    SubViewport? viewport;
    Task queue = Task.CompletedTask;
    readonly Dictionary<string, Texture2D> loaded = new();
    string? style;

    /// What goes into the cache name besides the part: the style version and the materials file, so recolouring a
    /// material re-renders the icons that use it.
    string Style => style ??= $"{StyleVersion} {FileAccess.GetFileAsString(MaterialFiles.Path).Sha256Text()}";

    public string CachePath(PartDef def) => $"{CacheDir}/{IconKey.For(def, Style)}.png";

    /// The part's icon: from the cache if it holds a readable image, otherwise rendered (one at a time) and saved.
    public Task<Texture2D> Get(PartDef def)
    {
        var next = queue.ContinueWith(_ => GetNow(def), TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
        queue = next;
        return next;
    }

    /// Forgets icons already handed out, so the next Get reads the cache (or renders) again.
    public void Forget() => loaded.Clear();

    async Task<Texture2D> GetNow(PartDef def)
    {
        var path = CachePath(def);
        if (loaded.TryGetValue(path, out var known))
            return known;
        var image = new Image();
        if (!FileAccess.FileExists(path) || image.Load(path) != Error.Ok || image.IsEmpty())
        {
            image = await Render(def);
            DirAccess.MakeDirRecursiveAbsolute(CacheDir);
            var err = image.SavePng(path);
            if (err != Error.Ok)
                GD.PushWarning($"Couldn't cache the icon for {def.Id}: {err}");
            else
                RemoveStale(def.Id, path);
        }
        var texture = ImageTexture.CreateFromImage(image);
        loaded[path] = texture;
        return texture;
    }

    /// Deletes this part's older cached icons (same id, other hash).
    static void RemoveStale(string partId, string keep)
    {
        var stale = new Regex($"^{Regex.Escape(partId)}-[0-9a-f]{{16}}\\.png$");
        foreach (var file in DirAccess.GetFilesAt(CacheDir))
            if (stale.IsMatch(file) && $"{CacheDir}/{file}" != keep)
                DirAccess.RemoveAbsolute($"{CacheDir}/{file}");
    }

        async Task<Image> Render(PartDef def)
    {
        if (viewport == null)
        {
            AddChild(viewport = new SubViewport
            {
                Size = new Vector2I(Size, Size) * 4, TransparentBg = true, OwnWorld3D = true,
                Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            });
        }
        foreach (var child in viewport.GetChildren())
        {
            viewport.RemoveChild(child);
            child.QueueFree();
        }

        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Environment
            {
                BackgroundMode = Environment.BGMode.ClearColor, // transparent; the sky is only for metal reflections
                Sky = StudioEnvironment.ReflectionSky(),
                ReflectedLightSource = Environment.ReflectionSource.Sky,
                AmbientLightSource = Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.55f, 0.6f),
                AmbientLightEnergy = 0.6f,
            },
        });

        var mesh = PartMeshFactory.Get(def);
        var bounds = mesh.GetAabb();
        var long_ = def.Length >= 3f * def.Width;
        var roller = new Node3D { RotationDegrees = new Vector3(0, 0, long_ ? LongRoll : 0f) };
        viewport.AddChild(roller);
        var holder = new Node3D { RotationDegrees = new Vector3(0, Yaw, 0) };
        roller.AddChild(holder);
        holder.AddChild(new MeshInstance3D { Mesh = mesh, Position = -bounds.GetCenter() });

        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, -35, 0), LightEnergy = 1.3f });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-10, 150, 0), LightEnergy = 0.4f });

        // Orthographic, from Elevation degrees above, sized so the whole part fits with a small margin.
        var reach = bounds.Size.Length();
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = reach * 0.8f };
        viewport.AddChild(camera);
        var dir = new Vector3(0, Mathf.Sin(Mathf.DegToRad(Elevation)), Mathf.Cos(Mathf.DegToRad(Elevation)));
        camera.LookAtFromPosition(dir * reach * 3f, Vector3.Zero);
        camera.Current = true;

        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = viewport.GetTexture().GetImage();
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        image.Resize(Size, Size, Image.Interpolation.Lanczos);
        return image;
    }
}
