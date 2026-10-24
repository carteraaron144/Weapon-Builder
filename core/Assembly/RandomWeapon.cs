namespace WeaponBuilder.Core;

/// How a random weapon is grown. The handle is depth 0; parts at MaxDepth get no children. Each optional socket is
/// filled with probability OptionalChance; required sockets always are (above the depth cap).
public sealed record RandomOptions(int MaxDepth = 3, double OptionalChance = 0.6);

/// Builds random weapons. The same library and seed always give the same weapon.
public static class RandomWeapon
{
    /// A random weapon: a random handle, then a walk down its sockets, picking a fitting part, a spin allowed by
    /// the socket, and a material (see PickMaterial). Sockets nothing fits stay empty. Throws if lib has no handles.
    public static Weapon Build(PartLibrary lib, MaterialLibrary materials, Random rng, RandomOptions? options = null)
    {
        options ??= new RandomOptions();
        var handles = Sorted(lib.All.Where(p => p.Tags.Contains(WeaponEditor.HandleTag)));
        if (handles.Count == 0)
            throw new InvalidOperationException("The part library has no handles");
        var handle = handles[rng.Next(handles.Count)];
        var root = new WeaponPart(handle.Id, PickMaterial(handle, materials, rng));
        Grow(root, handle, 0, lib, materials, rng, options);
        return new Weapon(WeaponJson.DefaultName, root);
    }

    static void Grow(WeaponPart part, PartDef def, int depth, PartLibrary lib, MaterialLibrary materials, Random rng,
        RandomOptions options)
    {
        if (depth >= options.MaxDepth)
            return;
        foreach (var socket in def.Sockets)
        {
            if (!socket.Required && rng.NextDouble() >= options.OptionalChance)
                continue;
            var fits = Sorted(lib.All.Where(p => Fitting.Accepts(socket, p)));
            if (fits.Count == 0)
                continue;
            var childDef = fits[rng.Next(fits.Count)];
            var step = socket.RotationStepDegrees;
            var spin = step > 0f ? step * rng.Next(0, (int)(360f / step)) : 0f;
            var child = new WeaponPart(childDef.Id, PickMaterial(childDef, materials, rng)) { RotationDegrees = spin };
            part.Children[socket.Name] = child;
            Grow(child, childDef, depth + 1, lib, materials, rng, options);
        }
    }

    /// The part's default half the time, otherwise a random panel material it allows (in file order, so seeds
    /// reproduce). The default if it allows none.
    static string PickMaterial(PartDef def, MaterialLibrary materials, Random rng)
    {
        if (rng.NextDouble() < 0.5)
            return def.DefaultMaterial;
        var allowed = materials.Visible.Where(m => Fitting.AllowsMaterial(def, m)).ToList();
        return allowed.Count == 0 ? def.DefaultMaterial : allowed[rng.Next(allowed.Count)].Id;
    }

    /// By id, so the result doesn't depend on the order part files were loaded in.
    static List<PartDef> Sorted(IEnumerable<PartDef> parts) => parts.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
}
