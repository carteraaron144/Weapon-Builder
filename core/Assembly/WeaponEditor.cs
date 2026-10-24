namespace WeaponBuilder.Core;

/// What an edit did. Replaced is the part that was in the socket; Dropped are its children
/// that couldn't move onto the new part.
public sealed record AttachResult(bool Ok, WeaponPart? Replaced, IReadOnlyList<WeaponPart> Dropped)
{
    public static readonly AttachResult Refused = new(false, null, []);
}

/// Attach, swap and detach rules for a weapon tree.
public sealed class WeaponEditor(PartLibrary lib)
{
    public const string HandleTag = "handle";

    /// Puts a new part of kind partId on the parent's socket (see Attach with a part).
    public AttachResult Attach(WeaponPart parent, string socket, string partId) =>
        lib.TryGet(partId, out var def)
            ? Attach(parent, socket, new WeaponPart(partId, def.DefaultMaterial))
            : AttachResult.Refused;

    /// Puts a part (with any children it has) on the parent's socket. Refused if the socket doesn't
    /// exist or doesn't accept the part. A part already there is replaced; its children move to the
    /// new part where it has a free socket of the same name that accepts them, and are dropped otherwise.
    public AttachResult Attach(WeaponPart parent, string socket, WeaponPart part)
    {
        if (!lib.TryGet(parent.PartId, out var parentDef) || !lib.TryGet(part.PartId, out var def))
            return AttachResult.Refused;
        var socketDef = parentDef.Sockets.FirstOrDefault(s => s.Name == socket);
        if (socketDef == null || !Fitting.Accepts(socketDef, def))
            return AttachResult.Refused;

        parent.Children.TryGetValue(socket, out var old);
        var dropped = old == null ? [] : CarryChildren(old, part);
        parent.Children[socket] = part;
        return new AttachResult(true, old, dropped);
    }

    /// Replaces the root with a new handle. Refused for parts without the handle tag.
    public AttachResult SwapRoot(Weapon weapon, string partId)
    {
        if (!lib.TryGet(partId, out var def) || !def.Tags.Contains(HandleTag))
            return AttachResult.Refused;
        var old = weapon.Root;
        var root = new WeaponPart(partId, def.DefaultMaterial);
        var dropped = CarryChildren(old, root);
        weapon.Root = root;
        return new AttachResult(true, old, dropped);
    }

    /// Removes the part on parent's socket with its subtree. Null if the socket is empty.
    public WeaponPart? Detach(Weapon weapon, WeaponPart parent, string socket) =>
        parent.Children.Remove(socket, out var removed) ? removed : null;

    /// Removes a part with its subtree. Null for the root, which can only be swapped.
    public WeaponPart? DetachPart(Weapon weapon, WeaponPart part)
    {
        var parent = FindParent(weapon, part, out var socket);
        return parent == null ? null : Detach(weapon, parent, socket);
    }

    /// The part whose socket holds child, or null if child is the root or not in the weapon.
    public WeaponPart? FindParent(Weapon weapon, WeaponPart child, out string socket)
    {
        var stack = new Stack<WeaponPart>([weapon.Root]);
        while (stack.Count > 0)
        {
            var part = stack.Pop();
            foreach (var (name, c) in part.Children)
            {
                if (ReferenceEquals(c, child))
                {
                    socket = name;
                    return part;
                }
                stack.Push(c);
            }
        }
        socket = "";
        return null;
    }

    /// Moves from's children onto to's free, same-named, accepting sockets. Returns the ones that didn't fit.
    List<WeaponPart> CarryChildren(WeaponPart from, WeaponPart to)
    {
        var dropped = new List<WeaponPart>();
        var toDef = lib.Get(to.PartId);
        foreach (var (name, child) in from.Children)
        {
            var socket = toDef.Sockets.FirstOrDefault(s => s.Name == name);
            if (socket != null && !to.Children.ContainsKey(name)
                && lib.TryGet(child.PartId, out var childDef) && Fitting.Accepts(socket, childDef))
                to.Children[name] = child;
            else
                dropped.Add(child);
        }
        from.Children.Clear();
        return dropped;
    }
}
