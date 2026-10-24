# MVP Weapon Builder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> The repo-root `CLAUDE.md` is currently a stale copy of another project's (The Mountain) — ignore it until Task 1 replaces it.

**Goal:** A Spore-style builder where the user drags parts from tabbed palettes onto sockets of a weapon, swaps and removes them, orbits the camera, and saves the finished weapon (JSON + thumbnail) to a gallery folder.

**Architecture:** All rules (part definitions, socket fitting, scaling, the weapon tree, edits, layout transforms, JSON) live in an engine-free C# library `WeaponBuilder.Core` using `System.Numerics`, unit-tested with xUnit via `dotnet test`. The Godot project (C#) is a thin view: it loads part JSON into the core, turns `Layout` output into nodes, and handles input. Parts are pure data: their meshes are lists of primitives merged into one flat-shaded mesh at runtime.

**Tech Stack:** Godot 4.7.2 .NET edition (Mobile renderer), C#, .NET SDK (the version Godot 4.7's C# docs require), xUnit, System.Text.Json.

**Spec:** [[Design]] (`Weapon Builder Docs/Design.md`, see its *Decisions* section).

## Global Constraints

- Godot **4.7.2 .NET** edition; renderer Mobile.
- Core library references **no Godot assembly**. Godot code converts with `NumericsBridge` (Task 6).
- Part scale = `min(socket.Length / part.Length, socket.Width / part.Width)`, clamped to **[0.25, 4]**.
- A socket accepts a part iff `socket.Accepts ∩ part.Tags ≠ ∅`.
- The root part must carry the tag `handle`; the root can be swapped, never removed.
- Part files: `data/parts/*.json`; files whose name starts with `_` are templates and skipped.
- Gallery: `user://gallery/<slug>-<yyyyMMdd-HHmmss>.json` + same-name `.png` (256×256).
- Art direction: low-poly, flat-shaded; object-space triplanar materials.
- Euler rotations in data are **degrees**, applied in Godot's default YXZ order (`Matrix4x4.CreateFromYawPitchRoll(y, x, z)` matches it).

## Review Focus

1. **Swapping a part that has children:** children whose socket name exists on the new part and is accepted there carry over; the rest are dropped (and returned so the UI could report it). → Task 3 tests.
2. **Dropping a part on a socket that doesn't accept it:** nothing changes; no snap highlight shown. → Task 3 test + Task 8 manual check.
3. **Loading a gallery file that names a part no longer in the library:** that subtree is dropped, the rest loads, a warning is returned; an unknown root falls back to the default handle. → Task 4 tests.
4. **Removing the root:** refused; dragging the root handle away does nothing. → Task 3 test.
5. **Extreme size mismatches** (great blade on a tiny socket): scale clamps to [0.25, 4]. → Task 2 test.

## File Structure

```
project.godot, WeaponBuilder.csproj, WeaponBuilder.sln   Godot project (editor-generated csproj, edited in Task 1)
core/                         (.gdignore)  WeaponBuilder.Core.csproj
  Parts/PartDef.cs            PartDef, SocketDef, Primitive records
  Parts/PartLibrary.cs        load + validate part JSON
  Assembly/Fitting.cs         Accepts, Scale
  Assembly/Weapon.cs          Weapon, WeaponPart tree
  Assembly/WeaponEditor.cs    attach / swap / detach rules
  Assembly/Layout.cs          world transforms of parts and sockets
  Serialization/WeaponJson.cs save/load weapon JSON
  Gallery/GalleryNames.cs     file naming
tests/WeaponBuilder.Core.Tests/  (.gdignore)  xUnit tests, one file per core file
data/parts/*.json             the 12 MVP parts + _template.json
src/                          Godot-side C#
  Util/NumericsBridge.cs      System.Numerics → Godot
  Meshes/MeshKit.cs           port of The Mountain's flat_mesh + primitives
  Meshes/PartMeshFactory.cs   Primitive list → ArrayMesh (cached per part id)
  Materials/WeaponMaterials.cs object-space triplanar steel/wood/leather
  Builder/WeaponView.cs       Layout → node tree, picking
  Builder/OrbitCamera.cs
  Builder/PartPalette.cs      tabbed part list
  Builder/DragController.cs   ghost, socket markers, snap, drop
  Builder/BuilderScreen.cs + scenes/builder.tscn   main scene, Build/New buttons
  Gallery/GallerySaver.cs     write JSON + thumbnail
assets/shaders/outline.gdshader   copied from The Mountain (item_outline.gdshader), hover highlight
tools/test.sh                 dotnet test
tools/shot.gd                 headless-ish screenshot of a scene for visual checks
```

---

### Task 0: Toolchain (needs the user's OK — downloads)

- [ ] **Step 1:** Ask the user to install (or approve installing) the .NET SDK version Godot 4.7 requires, and the **Godot 4.7.2 .NET** macOS build (alongside the existing standard `Godot.app`; e.g. `/Applications/Godot_mono.app`).
- [ ] **Step 2:** Verify: `dotnet --list-sdks` lists it; `/Applications/Godot_mono.app/Contents/MacOS/Godot --version` prints `4.7.2.stable.mono...`.

### Task 1: Project scaffolding + first core test

**Files:**
- Create: `project.godot`, `WeaponBuilder.csproj`, `WeaponBuilder.sln` (via the .NET editor: new project, Mobile renderer, Project → Tools → C# → Create C# solution; or headless equivalent)
- Create: `core/WeaponBuilder.Core.csproj`, `core/.gdignore`, `tests/WeaponBuilder.Core.Tests/WeaponBuilder.Core.Tests.csproj`, `tests/.gdignore`, `tools/test.sh`
- Create: `core/Parts/PartDef.cs`, `core/Assembly/Fitting.cs` (Accepts only), `tests/.../FittingTests.cs`
- Replace: `CLAUDE.md` with this project's (engine/version, layout above, `tools/test.sh`, core-has-no-Godot rule, decisions link to `Weapon Builder Docs/Design.md`, git: commit only when asked; feature branches + PR once a remote exists)

**Interfaces — Produces:**
```csharp
namespace WeaponBuilder.Core;
public sealed record Primitive(string Shape, Vector3 Size, Vector3 Position, Vector3 RotationDegrees, Vector3 Scale, int Sides);
public sealed record SocketDef(string Name, Vector3 Position, Vector3 RotationDegrees,
    IReadOnlyList<string> Accepts, float Length, float Width, bool Required, float RotationStepDegrees);
public sealed record PartDef(string Id, string Name, string Category, IReadOnlyList<string> Tags,
    float Length, float Width, string DefaultMaterial, IReadOnlyList<SocketDef> Sockets, IReadOnlyList<Primitive> Mesh);
public static class Fitting { public static bool Accepts(SocketDef socket, PartDef part); }
```
Convention: a part's origin is its attach point and it grows along **+Y**; a socket's +Y points away from its owner.

- [ ] **Step 1:** `WeaponBuilder.csproj`: add `<Compile Remove="core/**;tests/**" />` and `<ProjectReference Include="core/WeaponBuilder.Core.csproj" />`. Core and tests target the same framework as the Godot csproj. Add both projects to the `.sln`.
- [ ] **Step 2: Failing tests** `FittingTests`: `Accepts_SharedTag_True` (socket `[blade, head]`, part `[head]`), `Accepts_NoSharedTag_False`, `Accepts_EmptyAccepts_False`.
- [ ] **Step 3:** `tools/test.sh` runs `dotnet test tests/WeaponBuilder.Core.Tests "$@"` — expect FAIL (no `Fitting`).
- [ ] **Step 4:** Implement `Fitting.Accepts`; run `tools/test.sh` → PASS. Also open the Godot project headless once (`--headless --path . --build-solutions --quit`) → exits 0.
- [ ] **Step 5: Commit** `chore: scaffold Godot C# project, core library and tests`.

### Task 2: Scale fitting

**Files:** Modify `core/Assembly/Fitting.cs`; Test `tests/.../FittingTests.cs`

**Produces:** `public static float Fitting.Scale(SocketDef socket, PartDef part)`; `public const float MinScale = 0.25f, MaxScale = 4f;`

- [ ] **Step 1: Failing tests:** `Scale_UsesTighterAxis` (socket L1 W0.2, part L2 W0.1 → `0.5f`), `Scale_ClampsLow` (socket L0.1 W0.1, part L10 W10 → `0.25f`), `Scale_ClampsHigh` (socket L10 W10, part L0.1 W0.1 → `4f`), `Scale_Equal_IsOne`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS. **Step 5: Commit** `feat(core): fit-scale parts to sockets`.

### Task 3: Weapon tree and edit rules

**Files:** Create `core/Assembly/Weapon.cs`, `core/Assembly/WeaponEditor.cs`, `core/Parts/PartLibrary.cs` (in-memory only for now: `PartLibrary(IEnumerable<PartDef>)`, `Get(id)`, `TryGet`, `All`, `ByCategory`); Test `tests/.../WeaponEditorTests.cs`

**Produces:**
```csharp
public sealed class WeaponPart {
    public string PartId; public string Material; public float RotationDegrees;
    public Dictionary<string, WeaponPart> Children { get; } // key = socket name
}
public sealed class Weapon { public string Name; public WeaponPart Root; }
public sealed record AttachResult(bool Ok, WeaponPart? Replaced, IReadOnlyList<WeaponPart> Dropped);
public sealed class WeaponEditor(PartLibrary lib) {
    public AttachResult Attach(WeaponPart parent, string socket, string partId);
    public AttachResult SwapRoot(Weapon weapon, string partId);
    public WeaponPart? Detach(Weapon weapon, WeaponPart parent, string socket);  // never the root
    public WeaponPart? FindParent(Weapon weapon, WeaponPart child, out string socket);
}
```
New parts get `Material = def.DefaultMaterial`, `RotationDegrees = 0`.

- [ ] **Step 1: Failing tests** (build a tiny library in the test: `grip` [handle] with sockets `top` [blade, head], `guard` [crossguard], `bottom` [pommel]; `blade` [blade] with socket `tip` [head]; `axe` [head]; `bar` [crossguard]; `ball` [pommel]; `pike` [handle] with only `top`):
  - `Attach_EmptyCompatibleSocket_Adds`
  - `Attach_IncompatibleSocket_RefusesAndChangesNothing`
  - `Attach_UnknownSocket_Refuses`
  - `Attach_OccupiedSocket_ReplacesAndReturnsOld`
  - `Attach_Swap_CarriesCompatibleChildren` (blade with `tip`=axe swapped for another part that has a `tip` accepting head → axe kept)
  - `Attach_Swap_DropsIncompatibleChildren` (swapped for `axe`, which has no `tip` → axe returned in `Dropped`)
  - `SwapRoot_ToPike_KeepsTop_DropsGuardAndBottom`
  - `SwapRoot_NonHandle_Refuses`
  - `Detach_Root_ReturnsNull_TreeUnchanged`
  - `Detach_Child_RemovesSubtree`
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS. **Step 5: Commit** `feat(core): weapon tree with attach, swap and detach`.

### Task 4: Part files and weapon JSON

**Files:** Modify `core/Parts/PartLibrary.cs`; Create `core/Serialization/WeaponJson.cs`, `core/Gallery/GalleryNames.cs`, `data/parts/_template.json`; Tests `PartLibraryTests.cs`, `WeaponJsonTests.cs`, `GalleryNamesTests.cs`

**Produces:**
```csharp
public static PartLibrary PartLibrary.Load(IEnumerable<(string FileName, string Json)> files, out List<string> errors);
public static string WeaponJson.Write(Weapon w);
public static Weapon WeaponJson.Read(string json, PartLibrary lib, string defaultRootId, out List<string> warnings);
public static string GalleryNames.FileStem(string weaponName, DateTime at); // "<slug>-<yyyyMMdd-HHmmss>"
```
Part file (camelCase; vectors as `[x, y, z]`; `rotation` in degrees; `sides` only for prism/sphere):
```json
{ "id": "grip", "name": "Leather Grip", "category": "Handles", "tags": ["handle"],
  "length": 0.25, "width": 0.04, "defaultMaterial": "leather",
  "sockets": [ { "name": "top", "position": [0, 0.25, 0], "rotation": [0, 0, 0],
                 "accepts": ["blade", "head"], "length": 0.9, "width": 0.07,
                 "required": false, "rotationStep": 90 } ],
  "mesh": [ { "shape": "prism", "size": [0.02, 0.25, 0.02], "position": [0, 0.125, 0], "sides": 6 } ] }
```
Primitive `size` per shape: `box` = extents; `prism` = [top radius, height, bottom radius]; `sphere` = [radius, rings, _]. `scale` and `rotation` default to `[1,1,1]` / `[0,0,0]`.
Weapon file: `{ "version": 1, "name": "...", "root": { "part": "grip", "material": "leather", "rotation": 0, "children": { "top": { ... } } } }`.

- [ ] **Step 1: Failing tests:**
  - `Load_SkipsUnderscoreFiles`, `Load_DuplicateId_Error`, `Load_NonPositiveLength_Error`, `Load_SocketWithNoAccepts_Error`, `Load_ParsesSocketVectorsAndDefaults`
  - `RoundTrip_PreservesTree` (3-level weapon → Write → Read → same part ids, materials, rotations, socket keys)
  - `Read_UnknownChildPart_DropsSubtreeWithWarning`
  - `Read_UnknownRoot_UsesDefaultRootWithWarning`
  - `Read_ChildOnSocketThatNoLongerAccepts_DroppedWithWarning`
  - `FileStem_Slugifies` (`"Big  Axe!"`, 2026-10-24 14:05:09 → `"big-axe-20261024-140509"`), `FileStem_EmptyName_UsesWeapon`
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement with System.Text.Json. **Step 4:** Run → PASS. **Step 5: Commit** `feat(core): load part files, save and load weapons`.

### Task 5: Layout

**Files:** Create `core/Assembly/Layout.cs`; Test `LayoutTests.cs`

**Produces:**
```csharp
public sealed record PlacedPart(WeaponPart Part, PartDef Def, Matrix4x4 World, WeaponPart? Parent, string? Socket);
public sealed record PlacedSocket(WeaponPart Owner, SocketDef Socket, Matrix4x4 World, WeaponPart? Occupant);
public sealed record LayoutResult(IReadOnlyList<PlacedPart> Parts, IReadOnlyList<PlacedSocket> Sockets);
public static LayoutResult Layout.Compute(Weapon w, PartLibrary lib);
public static Matrix4x4 Layout.SocketLocal(SocketDef s);   // rotation then translation
```
Root world = identity. Child (System.Numerics row-vector order; leftmost applies first):
```csharp
world = Matrix4x4.CreateScale(Fitting.Scale(socket, def))
      * Matrix4x4.CreateRotationY(deg2rad(part.RotationDegrees))
      * SocketLocal(socket) * parentWorld;
```
`SocketLocal = CreateFromYawPitchRoll(y, x, z) * CreateTranslation(position)`.

- [ ] **Step 1: Failing tests** (assert with a 1e-5 tolerance helper): `Root_IsIdentity`, `Child_SitsAtSocketPosition`, `Child_ScaleInheritedByGrandchild` (grandchild world-space size = product of both scales), `SocketRotation_Applied` (socket rotated 90° about Z → child +Y maps to world −X), `PartRotation_SpinsAboutSocketAxis`, `Sockets_IncludeOccupiedAndEmpty`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS. **Step 5: Commit** `feat(core): compute part and socket transforms`.

### Task 6: Meshes, materials and the MVP parts

**Files:** Create `src/Util/NumericsBridge.cs`, `src/Meshes/MeshKit.cs`, `src/Meshes/PartMeshFactory.cs`, `src/Materials/WeaponMaterials.cs`, `data/parts/*.json` (12 parts), `scenes/part_gallery.tscn` + `src/Debug/PartGallery.cs`, `tools/shot.gd`; Test: `tests/.../DataPartsTests.cs`

**Consumes:** `PartDef`, `Primitive`, `PartLibrary.Load`.
**Produces:**
```csharp
public static Transform3D NumericsBridge.ToGodot(System.Numerics.Matrix4x4 m);
public static ArrayMesh MeshKit.FlatMesh(Dictionary<Material, List<(Mesh mesh, Transform3D xf)>> groups);
public static ArrayMesh PartMeshFactory.Get(PartDef def);           // cached by id, no material set
public static Material WeaponMaterials.Get(string id);             // "steel", "iron", "wood", "leather", "brass"; unknown → steel
public static PartLibrary PartFiles.LoadAll();                      // reads res://data/parts, pushes errors with GD.PushError
```
- `NumericsBridge.ToGodot`: rows of a System.Numerics matrix are Godot basis **columns**:
  `new Transform3D(new Basis(new Vector3(m.M11,m.M12,m.M13), new Vector3(m.M21,m.M22,m.M23), new Vector3(m.M31,m.M32,m.M33)), new Vector3(m.M41,m.M42,m.M43))`.
- `MeshKit.FlatMesh`: port of `~/Projects/the-mountain/tools/mesh_kit.gd` `flat_mesh` (lines 10–50; per-triangle normal, snap to 1e-4, flip to agree with source normal, skip degenerate, re-index). Primitives: box → `BoxMesh`, prism → `CylinderMesh` (radial segments = sides, 1 ring), sphere → `SphereMesh` (low segments).
- `WeaponMaterials`: `StandardMaterial3D`, `Uv1Triplanar = true`, `Uv1WorldTriplanar = false` (object space), flat albedo colours for now (textures are a later plan).
- Parts (id: tags; sockets): `grip` (handle; top[blade,head], guard[crossguard], bottom[pommel]); `long_haft` (handle; top[blade,head], bottom[pommel]); `bar_guard`, `curved_guard`, `disc_guard` (crossguard); `ball_pommel`, `spike_pommel`, `ring_pommel` (pommel); `straight_blade`, `broad_blade` (blade); `spear_head`, `axe_head` (head; `spear_head` also tag blade). The grip's `guard` and `top` sockets share its top end; guard parts are flat (thin in Y), so the blade passes through them visually.
- [ ] **Step 1: Failing test** `DataPartsTests.AllShippedPartsLoadWithoutErrors` (reads `../../data/parts` from the test project's directory) and `DataParts_EveryHandleHasATopSocket`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Write the JSON files and the Godot-side classes. `PartGallery` lays every part in a row with its default material. **Step 4:** `tools/test.sh` → PASS; render `scenes/part_gallery.tscn` with `tools/shot.gd` (Godot .NET, not headless, `-- --scene … --out <scratch>/parts.png`) and look at it: every part visible, flat-shaded, nothing inside-out.
- [ ] **Step 5: Commit** `feat: part meshes, materials and the MVP part set`.

### Task 7: Weapon view and orbit camera

**Files:** Create `src/Builder/WeaponView.cs`, `src/Builder/OrbitCamera.cs`, `scenes/builder.tscn`, `src/Builder/BuilderScreen.cs`; copy `assets/shaders/outline.gdshader` from `~/Projects/the-mountain/assets/shaders/item_outline.gdshader`.

**Produces:**
```csharp
public partial class WeaponView : Node3D {
    public void Show(Weapon w, PartLibrary lib);              // rebuilds children from Layout.Compute
    public LayoutResult? Current { get; }
    public WeaponPart? PartAt(Vector2 screenPos, Camera3D cam); // ray vs per-part StaticBody3D (box from mesh AABB)
    public void SetHovered(WeaponPart? part);                  // outline on/off
    public Aabb Bounds { get; }
}
public partial class OrbitCamera : Camera3D {
    [Export] public float Distance = 1.6f; public void Frame(Aabb bounds);
}   // right-drag orbits (pitch clamped ±85°), wheel zooms (0.4–6 m), pivot = bounds centre
public partial class BuilderScreen : Node { public Weapon Weapon; public PartLibrary Lib; public WeaponEditor Editor; public void Refresh(); }
```
Builder starts with a new weapon: root `grip`, name "Unnamed Weapon".
- [ ] **Step 1:** Implement. **Step 2:** Verify: run `scenes/builder.tscn`; a hard-coded test weapon (grip + bar_guard + straight_blade + ball_pommel, built in `_Ready` when launched with `-- --demo`) renders assembled correctly; orbit and zoom work; shot saved to scratch looks right.
- [ ] **Step 3: Commit** `feat: render weapons and orbit around them`.

### Task 8: Palette, drag and snap

**Files:** Create `src/Builder/PartPalette.cs`, `src/Builder/DragController.cs`; Modify `scenes/builder.tscn`, `BuilderScreen.cs`

**Consumes:** `PartLibrary.ByCategory`, `Fitting.Accepts`, `WeaponEditor.Attach/SwapRoot/Detach/FindParent`, `WeaponView.Current/PartAt`.
**Behaviour (the decisions this task fixes):**
- Palette: `TabContainer` on the left, one tab per category in order Handles, Guards, Pommels, Blades & Heads; each tab a list of buttons with the part name (icons are a later plan).
- Pressing a palette entry starts a drag: a ghost (part mesh, 50% alpha unshaded copy of its material) follows the cursor on the plane through the weapon centre facing the camera.
- During a drag every **compatible** socket shows a small sphere marker (empty: pale blue; occupied: amber, meaning "swap"); incompatible sockets show nothing. A dragged handle targets the root instead (marker at the root's origin).
- Snap: nearest compatible marker within **48 px** on screen; the ghost jumps to that socket's transform at the fitted scale. `R` while snapped rotates by the socket's `RotationStepDegrees` (no-op if 0).
- Release: snapped → `Attach` (or `SwapRoot`) then `Refresh()`; not snapped → nothing.
- Pressing an existing non-root part: `Detach` it and its subtree, and drag that subtree as the ghost; release on a compatible socket re-attaches it (with its children), release elsewhere removes it. Pressing the root does nothing.
- Hover (no drag): outline on the part under the cursor.
- [ ] **Step 1:** Implement. **Step 2:** Verify manually in the running scene, each as a checkbox in the commit message body: build grip+guard+blade+pommel; swap blade for axe_head (amber marker); drop a pommel on the top socket → no marker, nothing happens; drag the blade off → removed with its tip child; swap root to long_haft → guard dropped, blade kept; drag the root → nothing. **Step 3:** `tools/test.sh` still PASS. **Step 4: Commit** `feat: drag parts onto sockets, swap and remove`.

### Task 9: Build → gallery

**Files:** Create `src/Gallery/GallerySaver.cs`; Modify `scenes/builder.tscn`, `BuilderScreen.cs`

**Produces:** `public static string GallerySaver.Save(Weapon w, Image thumbnail)` → returns the file stem; writes `user://gallery/<stem>.json` (`WeaponJson.Write`) and `<stem>.png`.
- Toolbar: **Build** (opens a name `LineEdit` dialog, default current name; Save writes and shows "Saved <stem>" for 3 s), **New** (resets to a bare `grip`).
- Thumbnail: hide markers/outline, frame the camera on `WeaponView.Bounds`, wait one frame, grab the viewport image, crop to a centred square, resize to 256×256.
- [ ] **Step 1:** Implement. **Step 2:** Verify: Build twice with the same name → two files with different stems; open a JSON and check it matches the weapon; the PNG shows the weapon centred. Read one back via a temporary `--load <path>` debug arg into the builder → same weapon renders.
- [ ] **Step 3: Commit** `feat: save built weapons to the gallery`.

---

## Later plans (not in this one)

- Materials picker (per-part material swatches; textures via triplanar).
- Edges: blade edge curves + swept edge profiles (jagged, sharp, battered, sawed, bleeders).
- Randomize button (random walk; required sockets first; depth cap).
- Gallery browser (list, load, delete) and rendered part icons for the palette.
