# Extended Features Roadmap

This roadmap covers the four features after the [[2026-10-24-mvp-builder|MVP builder]]:

- materials;
- edges;
- randomize;
- the gallery browser, together with part icons.

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
| 2 | Materials picker | M | Adds part selection, which edges reuse. Randomize then picks materials too. |
| 3 | Gallery browser + part icons | M | Both are offscreen rendering and UI. Neither changes weapon rules. |
| 4 | Edges | L | The largest and riskiest piece (sweeping geometry along a curve). It goes last so the rest doesn't wait on it. |

---

## 0. MVP hardening

**Detailed plan:** [[2026-10-24-hardening-randomize]] (Tasks 1–5).

These are the minor issues the MVP final review deferred. Each fix gets a failing test or smoke check first.

- [ ] A `null` child in the weapon JSON drops that child with a warning instead of throwing.
- [ ] A missing default handle (`grip`) shows an error instead of crashing. The builder falls back to the first part tagged `handle`.
- [ ] Input is ignored while a thumbnail is being captured (`Capturing`).
- [ ] When a part moves to a socket with a different rotation step, its rotation snaps to that step.
- [ ] Zooming mid-drag re-snaps the ghost. Losing window focus cancels the drag.
- [ ] An empty file passed to `LoadFrom` gives "empty file", not "unreadable".
- [ ] `data/*` is added to the export filter. Export a macOS build once and check that parts load.

**Done when:** core tests and smoke checks are green, and the exported build opens and builds a weapon.

---

## 1. Randomize

A **Randomize** button replaces the current weapon with a random one.

**Detailed plan:** [[2026-10-24-hardening-randomize]] (Tasks 6–7).

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

## 2. Materials picker

Click a part, then click a swatch to change its material.

**Decisions**

- **Selection:** clicking a placed part (without dragging) selects it. The part gets a tinted outline, using the `OutlineShell` tint. Clicking empty space clears the selection. Selection is separate from hover.
- **Swatch bar:** it appears along the bottom while a part is selected. It shows one swatch per material the part allows.
  - Shift-click applies a material to every part in the same category.
- **Materials become data:** they move to `data/materials.json`. Each entry has:
  - `id`, `name`;
  - `color`, `metallic`, `roughness`;
  - an optional `texture` (a tiling image in `assets/textures/`) and `textureScale`;
  - `tags`, such as `metal`, `wood` or `wrap`.
- A part lists the material tags it allows (a new optional `materials` field, defaulting to all), so a leather blade isn't offered. Matching uses the same shared-tag rule as sockets.
- **The core** gets:
  - a `MaterialLibrary`, with the same load and validate pattern as `PartLibrary`;
  - `WeaponEditor.SetMaterial(part, id)`, which rejects materials the part doesn't allow.
- **Textures:** object-space triplanar, via `Uv1Triplanar` and `Uv1Scale` from `textureScale`. Textures are small (256–512 px) tiling images. Prefer CC0 sources or generated noise. Downloading any texture needs the user's OK first.
- The ghost material (drag preview) keeps working for textured materials.
- **Loading:** a material that is unknown or no longer allowed falls back to the part's default, with a warning.

**Tests (core):** the material file loads and validates; tag matching; `SetMaterial` accepts allowed and rejects disallowed materials; JSON reading falls back for unknown materials.

**Smoke checks:**

- Clicking a part selects it, and clicking a swatch changes its material.
- The material survives Build and Load.
- Shift-click recolours the whole category.
- Dragging still works on a selected part.

---

## 3. Gallery browser + part icons

### Gallery browser

- A **Gallery** button opens a full-screen overlay showing a grid of saved weapons (thumbnail and name), newest first.
- Clicking a card loads that weapon into the builder. Unsaved changes get a "Discard current weapon?" prompt first.
- Each card has a delete button with a confirmation. Deleting moves the `.json` and `.png` to the OS trash (`OS.MoveToTrash`), so it can be undone.
- **The core** gets `GalleryIndex.Entries(IEnumerable<string> fileNames) -> list of (stem, json, png?)`, sorted newest first. It pairs files by stem and lists a json without a png with a placeholder.
- An unreadable entry shows with a warning badge instead of being hidden.
- Building a weapon that was loaded from the gallery still saves a new entry. Overwriting is out of scope.

### Part icons

- Each palette button shows a rendered icon of its part, with the name as a tooltip, instead of just text.
- Icons are rendered at startup in an offscreen `SubViewport`, using the same studio lighting, default material and outline. Each part is framed by its bounds.
- They are cached in `user://icons/<part id>-<hash>.png`, where the hash is of the part's JSON, so editing a part re-renders its icon.
- The palette shows text while the icons render, then swaps the icons in.

**Tests (core):** `GalleryIndex` pairing, sorting, and orphan handling; the icon cache key changes when the part JSON changes.

**Smoke checks:**

- The gallery lists the weapons saved during the smoke run.
- Loading a card restores the weapon.
- Deleting removes the card and the files.
- Every palette button has a non-empty icon.

---

## 4. Edges

A selected blade gets an edge type picker: **sharp, jagged, battered, sawed, bleeders**. The edge is modelled along the blade's outline.

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
2. Should textures be downloaded (CC0) or generated as noise? [generated noise first; ask before any download]
3. Should the gallery have rename and overwrite? [no, out of scope]
4. Can edges go on non-blade parts, such as spiked guards? [blades and axe heads only for now]
