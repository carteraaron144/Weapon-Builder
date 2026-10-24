# Builder Layout (Icon Palette + Materials Panel) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** give the builder Spore's layout. Rendered part icons go on the left and a materials panel goes on the right. You drag a material onto a part to paint it, and Shift paints the whole category.

**Architecture:**
- **Core** (pure, test-first): materials become data (`MaterialLibrary`). It also gets the painting rules (`WeaponEditor.SetMaterial`, `SetMaterialForCategory`), material fallback when loading, material picks in Randomize, and the icon cache key (`IconKey`).
- **Godot side:**
  - `WeaponMaterials` builds Godot materials from the library.
  - `PartIcons` renders and caches icons, ported from The Mountain's `tools/render_icons.gd`.
  - `PartPalette` becomes an icon grid.
  - A new `MaterialPanel` holds the swatches.
  - `DragController` gains a paint mode.

**Tech Stack:** Godot 4.7.2 .NET, C# (`net10.0`), xUnit, System.Numerics, System.Text.Json, System.Security.Cryptography.

**Spec:** [[Design]] (Decisions: Layout, Material set) and [[2026-10-24-extended-features]] (section 2).

## Global Constraints

- Never reference Godot from `core/`.
- **Core changes are test-first:** run `tools/test.sh` and see the new test fail before writing the code.
- **Godot changes get a smoke check:** `$GODOT --path . res://scenes/builder_smoke.tscn` must exit 0. Build with `dotnet build WeaponBuilder.csproj` first, because it shows the compile errors Godot hides.
- **Saves:** the weapon JSON stays `version: 1`. Old saves must keep loading.
- **Panel materials, in this order:** bronze, iron, steel, gold, mithril, obsidian, wood.
- **Leather** is hidden (not in the panel). It is the grip's default only.
- **Every part takes every material.** The `materials` restriction field is supported but no part uses it, and restriction is not tested (user's call, 2026-10-24).
- **Icons:** orthographic, part turned 30° about Y, camera 20° above, rendered at 4× and scaled down, transparent background, key and fill lights as in `render_icons.gd`. Long parts (`Length >= 3 * Width`) are rolled 45°.
- **Randomize:** keep the part's default material with chance 0.5, otherwise pick a random panel material.
- **Branch and commits:** work on `feat/builder-layout`, cut from `main`. Commit at the end of each task, with descriptive multi-line messages ending with the Co-Authored-By line.
- **No downloads.** Materials are flat colours.

## Review Focus

These are the inputs most likely to bite that the spec doesn't spell out. Each has a check in the task named after it.

1. **A swatch released over the root** (the grip). It must paint the grip, unlike part drags, which ignore the root. This is tested in Task 9.
2. **A paint drag interrupted** by focus loss, Escape, or Randomize/New mid-drag. The previewed material must be undone and the weapon left unchanged. This is tested in Task 9.
3. **Preview left behind:** moving the cursor from one part to another while painting must restore the first part's own material. This is tested in Task 9.
4. **A corrupt or empty cached icon PNG:** it is re-rendered, not shown blank. This is tested in Task 7.
5. **An old save naming `brass`, or another unknown material:** it loads with the part's default and a warning. This is tested in Task 4.

---

### Task 0: Branch

- [x] **Step 1:** Run `git checkout -b feat/builder-layout main`.
- [x] **Step 2:** Commit the spec and this plan alone as `docs: plan the Spore-style builder layout`. Include `Weapon Builder Docs/Design.md` and the two plan files.

### Task 1: MaterialLibrary

**Files:**
- Create: `core/Materials/MaterialDef.cs`, `core/Materials/MaterialLibrary.cs`
- Test: `tests/WeaponBuilder.Core.Tests/MaterialLibraryTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record MaterialDef(string Id, string Name, Vector3 Color, float Metallic, float Roughness, IReadOnlyList<string> Tags, bool Hidden);`
    - `Color` is linear 0–1 RGB, parsed from `"#RRGGBB"`.
  - `public sealed class MaterialLibrary`:
    - `MaterialLibrary(IEnumerable<MaterialDef>)`
    - `static MaterialLibrary Load(string json, out List<string> errors)`
    - `IReadOnlyList<MaterialDef> All` (file order)
    - `IReadOnlyList<MaterialDef> Visible` (not hidden, file order)
    - `bool TryGet(string id, out MaterialDef def)`
    - `MaterialDef Get(string id)`
- **File format:** `{ "materials": [ { "id", "name", "color": "#RRGGBB", "metallic", "roughness", "tags": [...], "hidden"?: bool } ] }`.
- **Bad entries:** an entry that fails is skipped, with one error naming its index or id. Failures are a missing id, a duplicate id, or a colour that isn't `#RRGGBB`. Use the same `JsonOptions.Value` as parts.
- **Unreadable JSON:** gives an empty library and one error.

- [x] **Step 1: Write the failing tests.**
  - `Load_ReadsEveryFieldInFileOrder`: two entries load with their exact fields. `#FF8000` becomes `(1, 0.502, 0)` within 0.01.
  - `Load_DuplicateIdIsSkippedWithAnError`: the first entry is kept, and the error contains the id.
  - `Load_BadColourIsSkippedWithAnError`: `"orange"` and `"#12345"` are both rejected.
  - `Load_BrokenJsonGivesAnEmptyLibraryAndAnError`
  - `Visible_LeavesOutHiddenMaterials`
- [x] **Step 2:** Run `tools/test.sh --filter MaterialLibrary`. Expected: compile failure, because `MaterialLibrary` doesn't exist.
- [x] **Step 3:** Implement it. Use a private `MaterialFile` DTO, the same pattern as `PartFile`.
- [x] **Step 4:** Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat(core): materials as data`.

### Task 2: Shipped materials

**Files:**
- Create: `data/materials.json`
- Modify: `data/parts/*.json` (`"brass"` → `"bronze"`), `data/parts/_template.json` (the comment listing material ids)
- Test: `tests/WeaponBuilder.Core.Tests/DataMaterialsTests.cs`

**Interfaces:**
- Produces: the shipped ids.
- **Values** (tune after the Task 10 screenshots):

| id | name | color | metallic | roughness | tags | hidden |
|---|---|---|---|---|---|---|
| bronze | Bronze | #B0703A | 0.85 | 0.4 | metal | |
| iron | Iron | #5A5A5E | 0.7 | 0.6 | metal | |
| steel | Steel | #B4B8BF | 0.8 | 0.3 | metal | |
| gold | Gold | #E8B83A | 0.95 | 0.25 | metal | |
| mithril | Mithril | #C8DCF0 | 0.9 | 0.15 | metal | |
| obsidian | Obsidian | #141018 | 0.0 | 0.08 | stone | |
| wood | Wood | #73492B | 0.0 | 0.8 | wood | |
| leather | Leather | #54331F | 0.0 | 0.9 | leather | true |

- [x] **Step 1: Write the failing tests.**
  - Find the repo root the same way `DataPartsTests.LoadShipped` does, and move that walk into a shared `static class RepoFiles { public static string Root { get; } }` used by both test classes.
  - `ShippedMaterialsLoadWithoutErrors`
  - `Panel_IsTheSevenAgreedMaterialsInOrder`: `Visible` ids equal `["bronze","iron","steel","gold","mithril","obsidian","wood"]`.
  - `EveryPartsDefaultMaterialExists`: every shipped part's `DefaultMaterial` is in the library. This fails until brass is renamed.
- [x] **Step 2:** Run `tools/test.sh --filter DataMaterials`. Expected: FAIL, because there's no file and brass is unknown.
- [x] **Step 3:** Write `data/materials.json` and rename brass to bronze in the part files.
- [x] **Step 4:** Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat(data): the seven panel materials and hidden leather`.

### Task 3: Painting rules

**Files:**
- Modify: `core/Parts/PartDef.cs`, `core/Parts/PartFile.cs`, `core/Assembly/Fitting.cs`, `core/Assembly/WeaponEditor.cs`, `tests/.../TestParts.cs`
- Test: `tests/WeaponBuilder.Core.Tests/WeaponEditorTests.cs`

**Interfaces:**
- Produces:
  - **`PartDef`:** gains a last parameter, `IReadOnlyList<string>? Materials`. Null means any material. `PartFile` gains `List<string>? Materials` and passes it through. `TestParts.Part` passes `null`.
  - `public static bool Fitting.AllowsMaterial(PartDef part, MaterialDef material)`: null means true, otherwise the two share a tag.
  - `public bool WeaponEditor.SetMaterial(WeaponPart part, MaterialDef material)`: false, and nothing changes, if the part is unknown or `AllowsMaterial` is false. Otherwise it sets `part.Material = material.Id` and returns true.
  - `public int WeaponEditor.SetMaterialForCategory(Weapon weapon, string category, MaterialDef material)`: paints every part in the tree whose def has that category and allows the material. It returns how many were painted.
- **Deviation from the spec:** these take a `MaterialDef`, not an id. The caller looks the id up, so "unknown id" can't reach the editor, and `WeaponEditor`'s constructor stays as it is.

- [x] **Step 1: Write the failing tests.**
  - `SetMaterial_PaintsThePart`
  - `SetMaterialForCategory_PaintsOnlyThatCategory`: build a weapon of root `h` (category "Test") plus children in categories `A`, `A` and `B`. You'll need a `Part` overload or `with { Category = ... }`. The method returns 2, and the `B` part keeps its material.
  - `SetMaterialForCategory_IncludesTheRoot`: painting the root's category paints the root.
- [x] **Step 2:** Run `tools/test.sh --filter WeaponEditor`. Expected: compile failure.
- [x] **Step 3:** Implement it.
- [x] **Step 4:** Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat(core): paint a part or a whole category`.

### Task 4: Materials in saves

**Files:**
- Modify: `core/Serialization/WeaponJson.cs`, `tests/.../TestParts.cs`, `tests/.../WeaponJsonTests.cs`

**Interfaces:**
- Consumes: `MaterialLibrary` (Task 1) and `Fitting.AllowsMaterial` (Task 3).
- Produces: `WeaponJson.Read(string json, PartLibrary lib, MaterialLibrary materials, string defaultRootId, out List<string> warnings)`. This replaces the old signature, and every caller passes the library.
- **The rule:** a node's material is kept if it is known and allowed. Otherwise the part's default is used, with the warning `Unknown material '<id>' on '<part id>', using '<default>'`. A missing material is the default, with no warning.
- **`TestParts.Materials`:** a `MaterialLibrary` holding steel, iron and leather.

- [x] **Step 1: Write the failing tests.**
  - `Read_KeepsAKnownMaterial`
  - `Read_UnknownMaterialFallsBackToTheDefaultWithAWarning`: a save with `"material": "brass"` loads as `"steel"` (TestParts' default), with one warning containing `brass`.
  - Update the existing `Read` calls to pass `TestParts.Materials`.
- [x] **Step 2:** Run `tools/test.sh --filter WeaponJson`. Expected: compile failure, then FAIL on the fallback test.
- [x] **Step 3:** Implement it in the `Make` helper (line ~93) and the root path.
- [x] **Step 4:** Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat(core): unknown materials in saves fall back to the part's default`.

### Task 5: Randomize picks materials

**Files:**
- Modify: `core/Assembly/RandomWeapon.cs`, `tests/.../RandomWeaponTests.cs`

**Interfaces:**
- Produces: `RandomWeapon.Build(PartLibrary lib, MaterialLibrary materials, Random rng, RandomOptions? options = null)`.
- **Per part, after its spin:** if `rng.NextDouble() < 0.5`, use the default. Otherwise pick uniformly from `materials.Visible`, filtered by `AllowsMaterial` and kept in file order. If none is allowed, use the default.

- [x] **Step 1: Write the failing tests.**
  - `SameSeed_GivesTheSameMaterials`
  - `Materials_AreAlwaysKnown`: over 20 seeds, every part's material is in `TestParts.Materials` or is its default.
  - `SomeParts_GetANonDefaultMaterial`: across 20 seeds, at least one part's material differs from its default.
  - Update `Make` and the other `Build` calls.
- [x] **Step 2:** Run `tools/test.sh --filter RandomWeapon`. Expected: compile failure, then FAIL on `SomeParts_...`.
- [x] **Step 3:** Implement it.
- [x] **Step 4:** Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat(core): Randomize picks materials`.

### Task 6: Godot materials from data

**Files:**
- Create: `src/Materials/MaterialFiles.cs`
- Modify: `src/Materials/WeaponMaterials.cs`, `src/Builder/BuilderScreen.cs`, `src/Debug/BuilderSmoke.cs`

**Interfaces:**
- Consumes: Tasks 1, 4 and 5.
- Produces:
  - `MaterialFiles.Path = "res://data/materials.json"` and `MaterialFiles.Load() -> MaterialLibrary`. Errors go to `GD.PushError`.
  - `WeaponMaterials.Library`: a static `MaterialLibrary`, loaded lazily via `MaterialFiles.Load()`.
  - `WeaponMaterials.Get(id)` builds a triplanar `StandardMaterial3D` from the def. An unknown id gets `steel`.
  - `WeaponMaterials.Ids` is removed.
  - `BuilderScreen.Materials => WeaponMaterials.Library`. It is passed to `WeaponJson.Read` and `RandomWeapon.Build`.

- [x] **Step 1: Add a smoke check** (in `Run`, before the first palette drag): `WeaponMaterials.Library.Visible.Count == 7` and `WeaponMaterials.Get("mithril")` is not the steel material.
- [x] **Step 2:** Run `dotnet build WeaponBuilder.csproj`. Expected: compile errors at the old `Read`/`Build` calls. Fix them, then run the smoke scene. Expected: it fails only on the new check.
- [x] **Step 3:** Implement it.
- [x] **Step 4:** Run the smoke scene. Expected: exit 0. Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat: Godot materials come from data/materials.json`.

### Task 7: Part icons and the icon palette

**Files:**
- Create: `core/Parts/IconKey.cs`, `src/Parts/PartIcons.cs`
- Modify: `src/Builder/PartPalette.cs`, `src/Builder/BuilderScreen.cs`, `src/Debug/BuilderSmoke.cs`
- Test: `tests/WeaponBuilder.Core.Tests/IconKeyTests.cs`

**Interfaces:**
- **Core:** `public static string IconKey.For(PartDef def, string style)` returns `"<id>-<first 16 hex chars of SHA-256>"`. The hash is over `JsonSerializer.Serialize(def, new JsonSerializerOptions { IncludeFields = true })` + `"\n"` + `style`. `IncludeFields` matters: `Vector3`'s X, Y and Z are fields, and without it they serialize as `{}`.
- **Godot:** `public partial class PartIcons : Node`:
  - Constants:
    - `Size = 72`, the icon's final size in pixels;
    - `Style = "v1 yaw30 elev20 roll45"`, plus a hash of the materials file text;
    - `CacheDir = "user://icons"`.
  - `async Task<Texture2D> Get(PartDef def)`:
    1. Load `CacheDir/<key>.png` if it exists and decodes to a non-empty image.
    2. Otherwise render the part and save it.
    3. Return an `ImageTexture`.
  - **Rendering** (port of `render_icons.gd`'s `_render`):
    - one reused `SubViewport` with `Size * 4`, `TransparentBg`, `OwnWorld3D` and `MSAA 4×`;
    - the mesh is `PartMeshFactory.Get(def)` (default material), centred on its AABB;
    - holder yaw 30°, roller roll 45° when `def.Length >= 3 * def.Width`;
    - the same key and fill lights and ambient as the GDScript;
    - an orthographic camera with `Size = aabb.Size.Length() * 0.8`, at elevation 20°;
    - wait for 2 `FramePostDraw` signals, then resize to `Size` with Lanczos.
  - Renders run one at a time, since there is one viewport.
- **Palette:**
  - Each tab is a `GridContainer { Columns = 3, Name = <category> }`, so the smoke path `"{category}/{partId}"` still resolves.
  - Each button is 76×76, with `TooltipText` set to the name, `ExpandIcon = true`, and its text set to the part name until `SetIcon(string partId, Texture2D icon)` replaces the text with the icon.
  - `BuilderScreen` adds a `PartIcons` child and `public Task IconsReady`. That task fills icons palette tab by palette tab after `_Ready`.

- [x] **Step 1: Write the failing core tests.**
  - `IconKey_StartsWithThePartId`
  - `IconKey_IsStableForTheSamePart`
  - `IconKey_ChangesWhenAPrimitiveSizeChanges`: change a `Vector3` in the mesh. This catches the `IncludeFields` trap.
  - `IconKey_ChangesWithTheStyle`
- [x] **Step 2:** Run `tools/test.sh --filter IconKey`. Expected: compile failure, then everything passes after Step 3.
- [x] **Step 3:** Implement `IconKey`.
- [x] **Step 4: Add smoke checks** at the start of `Run`, after `await b.IconsReady`:
  - Every palette button has an `Icon` with width > 0 and empty `Text`.
  - **Corrupt cache** (Review Focus 4): write the bytes `"junk"` to the cache path for `straight_blade`. A fresh `await icons.Get(def)` returns a non-empty texture, and the file on disk is now a valid PNG.
  - Take a screenshot of the layout: `Shot("layout.png")`.
- [x] **Step 5:** Run `dotnet build WeaponBuilder.csproj` and the smoke scene. Expected: it fails on the new checks. Implement `PartIcons` and the palette changes. Run the smoke scene again. Expected: exit 0. Look at `layout.png` and check the angle against The Mountain's icons.
- [x] **Step 6:** Run `tools/test.sh`. Expected: everything passes. Commit as `feat: rendered part icons in a grid palette`.

### Task 8: Materials panel

**Files:**
- Create: `src/Builder/MaterialPanel.cs`
- Modify: `src/Builder/BuilderScreen.cs`, `src/Debug/BuilderSmoke.cs`

**Interfaces:**
- Produces: `public partial class MaterialPanel : VBoxContainer`, with:
  - `event Action<string>? MaterialPressed`, raised on `ButtonDown`;
  - `void Fill(MaterialLibrary lib)`.
- **Swatch buttons:** one per `lib.Visible`, named by id.
  - Text is the material name, with the icon above the text.
  - The icon is a generated 40 px `ImageTexture`: a filled circle of the material colour, darkened towards the lower right so metals read as round.
- **Placement:** `BuilderScreen.MaterialPanel` sits on the right-wide anchor, at `OffsetRight = -12`, below the toolbar (`OffsetTop = 56`), and about 96 px wide. `MaterialPressed` is wired to `Drag.BeginPaint` (Task 9). Until then, wire it to nothing.

- [x] **Step 1: Add smoke checks.** The panel's child button names equal the seven ids in order. Each button sits right of the 3D view's centre and doesn't overlap the toolbar's rect.
- [x] **Step 2:** Run the smoke scene. Expected: FAIL.
- [x] **Step 3:** Implement it.
- [x] **Step 4:** Run the smoke scene. Expected: exit 0. Commit as `feat: materials panel on the right`.

### Task 9: Dragging a swatch onto a part

**Files:**
- Modify: `src/Builder/DragController.cs`, `src/Builder/WeaponView.cs`, `src/Builder/BuilderScreen.cs`, `src/Debug/BuilderSmoke.cs`

**Interfaces:**
- Consumes: `WeaponEditor.SetMaterial` / `SetMaterialForCategory` (Task 3) and `MaterialPanel.MaterialPressed` (Task 8).
- **`WeaponView`:**
  - `void Preview(IEnumerable<WeaponPart> parts, string? materialId)` sets those parts' mesh `MaterialOverride` to `materialId`.
  - `ClearPreview()` restores every previewed part to its own material.
  - Preview and hover outline are independent.
- **`DragController`:**
  - `public string? Painting`, the material id being dragged. `Dragging` stays part-only.
  - `public void BeginPaint(string materialId)` calls `Cancel()` first.
  - **While painting:**
    - The part under the cursor previews the material, and so does the root (unlike part drags).
    - With Shift held (`Input.IsKeyPressed(Key.Shift)`, or the event's `ShiftPressed`), every part in that part's category previews it.
  - **Release (left up):**
    - Over a part: `SetMaterial`, or `SetMaterialForCategory` when Shift is held. Then `Refresh()` and the status `Painted <part name> <material name>` (or `Painted every <category> ...`).
    - Over nothing: no change.
  - **Escape** while painting or dragging calls `Abort()`.
  - `Cancel()` / `Abort()` also clear `Painting` and call `View.ClearPreview()`, so focus loss, New, Randomize and Load all clear it.
- **Smoke helpers:** `Mouse` gains `bool shift = false`, which sets `ShiftPressed` on the event. Add `Paint(string materialId, Vector2 to, bool shift = false)`: press on the swatch, move in 3 steps, then release.

- [x] **Step 1: Add a smoke section `Painting()`** after `Randomize()`, starting from a weapon with a blade on `top`, an axe head on the blade's `tip`, a guard and a pommel:
  - Painting gold onto the blade paints the blade gold, and the axe head keeps its material.
  - Painting onto empty space changes no part's material.
  - Painting iron onto the grip (the root) paints the grip iron. (Review Focus 1)
  - Shift-painting mithril onto the blade paints the blade and the axe head (both "Blades & Heads"), and the guard is unchanged.
  - **Leftover preview** (Review Focus 3): press on obsidian, move over the blade, then over the guard. The blade's `MaterialOverride` is its own material again. Releasing off the weapon leaves every material unchanged.
  - **Interrupted paint** (Review Focus 2): press on gold and hover the blade, then send `NotificationApplicationFocusOut`. `Painting` is null and the blade's mesh shows its own material. Repeat with Escape. Then, mid-paint, call `b.Randomize(7)`. `Painting` is null, and the materials map equals that of a clean `Randomize(7)` taken just before.
  - **Survives Build and Load:** after `Build("Painted")` and `LoadFrom` of the saved file, the materials map (part path → material) is identical.
  - **Part drags still work:** dragging a pommel onto `bottom` after painting still attaches.
- [x] **Step 2:** Run the smoke scene. Expected: FAIL on the new checks.
- [x] **Step 3:** Implement it.
- [x] **Step 4:** Run the smoke scene. Expected: exit 0. Run `tools/test.sh`. Expected: everything passes.
- [x] **Step 5:** Commit as `feat: drag a material onto a part (Shift for its category)`.

### Task 10: Screenshots, docs, finish

**Files:**
- Modify: `src/Debug/BuilderSmoke.cs`, `data/materials.json` (tuning only), `CLAUDE.md`, the roadmap, and this plan.

- [x] **Step 1: Material screenshots.** With `--out`, the smoke run paints the demo sword in each visible material, using `SetMaterialForCategory` on every category, and saves `material-<id>.png`. Run it to a scratch directory and look at all seven plus `layout.png`. Tune the colours, metallic and roughness in `data/materials.json` until each one reads as distinct (obsidian black and glassy, mithril pale blue). Re-run `tools/test.sh`.
- [x] **Step 2: Docs.**
  - `CLAUDE.md` Layout section: mention `data/materials.json` and the icon cache in `user://icons`.
  - Tick roadmap section 2 and mark it **Done**.
  - Tick this plan's boxes and add an Outcome section.
- [x] **Step 3:** Commit as `docs: builder layout done`.
- [x] **Step 4: Final whole-branch review** (fresh reviewer, most capable model), as in executing-plans.

---

## Outcome

All tasks done on `feat/builder-layout`. Core tests: 95/95. The smoke scene passes every check.

**Changes from the plan:**
- **Palette tabs show their first part's icon** instead of their name, with the category name as the tooltip. The text titles forced the palette to 340 px; it is now 224 px.
- **Metals reflect a light studio sky** (`StudioEnvironment.ReflectionSky`) in both the icons and the builder view. Against a transparent or dark background they rendered near-black. The view's background stays plain grey.
- **The icon cache deletes a part's older icons** when it saves a new one, so `user://icons` doesn't grow with every edit.
- **Swatches are 32 px**, so all seven fit a 1152×648 window. A smoke check guards this.
- **While painting**, the target part also gets the hover outline.
- **Material values were tuned from the screenshots:**
  - iron is lighter (`#6E6E74`), so it doesn't look like obsidian;
  - gold is brighter and a little less metallic (`#FFCC3A`, 0.75 / 0.3);
  - obsidian has a purple tint and a slight sheen (`#1E1628`, 0.3 / 0.05).
