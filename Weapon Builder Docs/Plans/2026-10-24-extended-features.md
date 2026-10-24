# Extended Features Roadmap

This roadmap covers the features after the [[2026-10-24-mvp-builder|MVP builder]]:

- randomize;
- the Spore-style layout: an icon palette and a materials panel;
- the gallery browser;
- edges.

Each feature gets its own detailed implementation plan, with exact signatures and tests, when its turn comes. This page fixes the scope, the decisions, the order, and what "done" means for each.

**Spec:** [[Design]], especially the Decisions section.

## Global rules (all features)

- Weapon rules go in `core/`, written test-first. Godot code goes in `src/` and is checked with the builder smoke scene plus screenshots.
- **Old saves keep loading.** Each feature adds optional fields to the weapon JSON, so files stay at `version: 1`. A missing field means the default. An unknown value is dropped and reported as a warning, the same way unknown parts are handled now.
- Art stays low-poly and flat-shaded, with object-space triplanar materials.
- One branch per feature (`feat/<name>`). Merge to `main` when its plan is done and reviewed.

## Order

| # | Feature | Size | Why this order |
|---|---|---|---|
| 0 | MVP hardening | S | Fixes the deferred minor issues before new code builds on them. |
| 1 | Randomize | S | Mostly core and easy to test; gives a quick fun win. It also makes many weapons for testing the later features. |
| 2 | Builder layout: icon palette + materials panel | M | The Spore-style layout everything else sits in. Randomize then picks materials too. |
| 3 | Gallery browser | S | UI only; it reuses the icon rendering from step 2. |
| 4 | Edges | L | The largest and riskiest piece (sweeping geometry along a curve). It goes last so the rest doesn't wait on it. |

---

## 0. MVP hardening

**Detailed plan:** [[2026-10-24-hardening-randomize]] (Tasks 1–5). **Done.**

These are the minor issues the MVP final review deferred. Each fix gets a failing test or smoke check first.

- [x] A `null` child in the weapon JSON drops that child with a warning instead of throwing.
- [x] A missing default handle (`grip`) shows an error instead of crashing. The builder falls back to the first part tagged `handle`.
- [x] Input is ignored while a thumbnail is being captured (`Capturing`).
- [x] When a part moves to a socket with a different rotation step, its rotation snaps to that step.
- [x] Zooming mid-drag re-snaps the ghost. Losing window focus cancels the drag.
- [x] An empty file passed to `LoadFrom` gives "empty file", not "unreadable".
- [x] `data/*` is added to the export filter. Export a macOS build once and check that parts load.

**Done when:** core tests and smoke checks are green, and the exported build opens and builds a weapon.

---

## 1. Randomize

A **Randomize** button replaces the current weapon with a random one.

**Detailed plan:** [[2026-10-24-hardening-randomize]] (Tasks 6–7). **Done.** It uses each part's default material until the materials picker exists.

**Decisions**

- The core gets `RandomWeapon.Build(PartLibrary lib, Random rng, RandomOptions options) -> Weapon`. It is a pure function, and the same seed gives the same weapon.
- It walks down the tree from a random handle:
  1. Fill every required socket.
  2. Fill each optional socket with chance `OptionalChance` (default 0.6).
  3. Stop below `MaxDepth` (default 3; the handle is depth 0).
- A part is only chosen if `Fitting.Accepts` allows it. A socket with no fitting part is left empty.
- It picks a material per part: the part's default with chance 0.5, otherwise a random compatible material (once materials exist). After step 4 it also picks an edge per blade.
- It picks a random rotation step where the socket allows one.
- The builder runs it with a fresh seed each click and shows the seed in the status line. A `--seed <n>` argument reproduces a weapon.

**Tests (core)**

- The same seed gives an identical weapon; different seeds differ across 20 tries.
- Every required socket is filled whenever any part fits it.
- No part sits deeper than `MaxDepth`.
- Every placed part is accepted by its socket.
- A library with only handles gives a bare handle.

**Smoke checks:** Randomize changes the weapon, the camera frames it, and Build saves it.

**Data:** mark the sockets that should always be filled as `"required": true`. At least the blade or head on `top` should be. No part uses `required` yet.

---

## 2. Builder layout: icon palette + materials panel

The builder takes Spore's layout: part icons on the left, the weapon in the middle, materials on the right. This step merges the old "materials picker" and "part icons" features (agreed 2026-10-24).

**Detailed plan:** [[2026-10-24-builder-layout]]. **Done.**

### Layout

- **Left:** the part palette, narrower than Spore's. It keeps the four category tabs, but each tab is a grid of icon buttons, 3 across at about 72 px. The part name is the tooltip.
- **Centre:** the 3D view, unchanged.
- **Right:** a slim materials column. Each material is a round swatch with its name under it.
- **Top:** the toolbar (New, Randomize, Build) stays where it is.

### Part icons

These are ported from The Mountain's `tools/render_icons.gd`.

- **Angle:** an orthographic camera, with the part turned 30° about Y and seen from 20° above. A key light and a fill light, on a transparent background. Rendered at 4× size and scaled down for clean edges.
- **Framing:** each part is framed by its bounds. Long, thin parts (length several times the width: blades, hafts) are rolled 45° so they run corner to corner.
- **Rendering:** icons render at startup in an offscreen `SubViewport`, with each part in its default material.
- **Cache:** icons are cached in `user://icons/<part id>-<hash>.png`, where the hash is of the part's JSON, so editing a part re-renders its icon.
- **Until ready:** buttons show the part name until their icon is ready.

### Materials

- **Materials are data:** they live in `data/materials.json`. Each entry has `id`, `name`, `color`, `metallic`, `roughness`, `tags` (`metal` or `wood`), and an optional `hidden`.
- **The panel lists seven:** bronze, iron, steel, gold, mithril, obsidian, wood.
  - Bronze is warm orange-brown; iron dark grey and fairly rough; steel bright grey; gold yellow and glossy.
  - Mithril is pale blue-silver with a slight sheen; obsidian near-black, glassy and non-metallic; wood brown and matte.
  - Flat colours for now. Generated noise textures may come later. Nothing is downloaded.
- **Leather is "basic":** it is a hidden material, used only as the grip's default. It is not in the panel. Painting the grip with a panel material is allowed.
- **Brass becomes bronze** in the part data. An old save naming `brass`, or any unknown material, falls back to the part's default with a warning.
- **Restriction is supported but unused:**
  - A part may list the material tags it allows (an optional `materials` field). Missing means any material.
  - No part uses it yet, so every part takes every material. Restriction is not tested in this step.

### Applying a material

- **Drag a swatch** out of the panel. The part under the cursor previews that material.
  - Releasing over a part paints it.
  - Releasing over empty space, or pressing Escape, cancels.
- **Shift while releasing** paints every part in the same category.
- There is no separate selection step. Swatch drags reuse the part picking that part drags already do.

### Core

- `MaterialLibrary`, with the same load and validate pattern as `PartLibrary`: unique ids, colour parsing, known fields.
- `PartDef.Materials` (optional tag list) and `Fitting.AllowsMaterial(PartDef, MaterialDef)`, which uses the shared-tag rule with "missing means all".
- `WeaponEditor.SetMaterial(node, id)` returns false for an unknown or disallowed material and leaves the part unchanged.
- `WeaponEditor.SetMaterialForCategory(category, id)` paints every part in that category.
- JSON reading falls back to the part's default for unknown materials, with a warning.
- **Randomize** picks a material per part: the default with chance 0.5, otherwise a random allowed material, in a fixed order so seeds stay reproducible.

### Tests (core)

- The material file loads; bad entries (duplicate id, bad colour) are reported.
- `SetMaterial` changes a part's material; an unknown id is refused.
- Category painting changes only that category.
- An unknown material in a save falls back to the part's default, with a warning.
- Randomize is still reproducible by seed and only uses known materials.
- Every part's `defaultMaterial` exists in `data/materials.json`.

### Smoke checks

- Every palette button gets a non-empty icon.
- The panel shows the seven materials.
- Dragging a swatch onto a blade paints it.
- Dropping on empty space changes nothing.
- Shift-drop paints the whole category.
- Materials survive Build and Load.
- Part drags still work after a swatch drag.

### Screenshots

The full layout, plus a weapon in each material.

---

## 3. Gallery browser

- A **Gallery** button opens a full-screen overlay showing a grid of saved weapons (thumbnail and name), newest first.
- Clicking a card loads that weapon into the builder. Unsaved changes get a "Discard current weapon?" prompt first.
- Each card has a delete button with a confirmation. Deleting moves the `.json` and `.png` to the OS trash (`OS.MoveToTrash`), so it can be undone.
- **The core** gets `GalleryIndex.Entries(IEnumerable<string> fileNames) -> list of (stem, json, png?)`, sorted newest first. It pairs files by stem and lists a json without a png with a placeholder.
- An unreadable entry shows with a warning badge instead of being hidden.
- Building a weapon that was loaded from the gallery still saves a new entry. Overwriting is out of scope.

**Tests (core):** `GalleryIndex` pairing, sorting, and orphan handling.

**Smoke checks:**

- The gallery lists the weapons saved during the smoke run.
- Loading a card restores the weapon.
- Deleting removes the card and the files.

---

## 4. Edges

The right-hand panel gets an **Edges** tab next to Materials, with five edge types: **sharp, jagged, battered, sawed, bleeders**. You drag an edge onto a blade, the same way materials are applied. The edge is modelled along the blade's outline.

**Decisions**

- **The edge curve is part data.** A blade (or axe head) gets an optional `edge` field: a polyline in part space, plus which way is "outward" (a normal hint). It runs along the cutting side or sides. A part without `edge` can't take edge types.
- **Edge types are data** in `data/edges/*.json`, and come in two kinds:
  - **Profile:** a 2D cross-section swept along the curve. Covers sharp, battered and bleeders. Bleeders use a profile with a groove, sitting inset from the edge.
  - **Repeat:** a small piece (made of mesh primitives) placed every `spacing` along the curve and aligned to it. Covers jagged and sawed teeth.
  - Both kinds can carry a `jitter` value (a seeded random offset per vertex or piece) for the battered look.
- **Geometry lives in the core:** `EdgeSweep.Build(EdgeCurve curve, EdgeType type, int seed) -> MeshData`, with plain vertex and index lists. That makes the hard part unit-testable. The Godot side only converts the result to a `FlatMesh`.
- **Each part's edge scales with the part.** It is part of the part's mesh node, so socket scaling applies to it for free.
- The weapon JSON gets an optional `edge: "<type id>"` per part. Randomize picks one for blades.
- The blade base mesh may need an "edgeless" version, so a toothed edge replaces the flat edge instead of sitting on top of it. Decide this in the edges plan after a screenshot test with one blade.

**Tests (core):**

- Sweeping along a straight line gives the expected vertex count and bounds.
- Repeat spacing gives `floor(length / spacing)` pieces.
- Pieces follow the curve's tangent at corners.
- The same seed gives identical jitter.
- Edge type files load and validate.
- Unknown edges in a save fall back to none, with a warning.

**Smoke checks and screenshots:** each edge type on `straight_blade`, `broad_blade` and `axe_head`; the edge survives a swap to another blade (when the new blade has an `edge` curve) and survives Build and Load.

---

## Open questions

Each is decided in its feature's plan; the default in brackets applies if no one decides otherwise.

1. Should Randomize ever change the handle, or keep the current one? [random handle]
2. Should textures be downloaded (CC0) or generated as noise? [flat colours in step 2; generated noise later; ask before any download]
3. Should the gallery have rename and overwrite? [no, out of scope]
4. Can edges go on non-blade parts, such as spiked guards? [blades and axe heads only for now]
